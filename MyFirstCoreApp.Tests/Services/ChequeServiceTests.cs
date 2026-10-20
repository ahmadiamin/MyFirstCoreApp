using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Banking;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class ChequeServiceTests : IDisposable
{
    private const int TenantA = 1;
    private const decimal Amount = 1_000_000;

    private const string ReceivedInstrument = "چک‌های دریافتنی";
    private const string IssuedInstrument = "چک‌های پرداختنی";
    private const string BankName = "بانک ملت";

    private readonly TestDatabase _db = new();

    // 2026-03-20 21:00 UTC = 2026-03-21 00:30 به وقت ایران ← «امروز» = 2026-03-21
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly IranToday = new(2026, 3, 21);

    public ChequeServiceTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // ثبت: سند ثبت چک بلافاصله ساخته می‌شود
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_Received_RegistersLedgerImmediately_AtIssueDate()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(contact));

        await using var verify = _db.NewContext();
        var cheque = await verify.Cheques.SingleAsync();
        var document = await verify.Transactions.Include(t => t.Lines).SingleAsync();

        Assert.Equal(ChequeStatus.InProgress, cheque.Status);
        Assert.Equal(document.Id, cheque.RegistrationTransactionId);
        Assert.Null(cheque.TransactionId);

        Assert.Equal("Cheque", document.ReferenceType);
        Assert.Equal(created.Id, document.ReferenceId);
        Assert.Equal(new DateOnly(2026, 3, 10), document.Date);   // تاریخ صدور
        Assert.True(document.IsPosted);
        Assert.Equal(Amount, document.TotalAmount);

        // دریافتنی: حساب چک بدهکار، طرف حساب (دریافتنی) بستانکار
        Assert.Equal(Amount, await BalanceAsync(ReceivedInstrument));
        Assert.Equal(-Amount, await ContactBalanceAsync(contact));

        var history = await verify.ChequeHistories.SingleAsync();
        Assert.Equal(ChequeHistoryAction.Created, history.Action);
        Assert.Equal("user-1", history.UserId);
    }

    [Fact]
    public async Task Create_Issued_RegistersMirrorEntry()
    {
        var contact = await AddContactAsync("فروشنده");
        await CreateAsync(NewRequest(contact, ChequeDirection.Issued));

        // پرداختنی: طرف حساب بدهکار (بدهی ما کم شد)، حساب چک پرداختنی بستانکار
        Assert.Equal(-Amount, await BalanceAsync(IssuedInstrument));
        Assert.Equal(Amount, await ContactBalanceAsync(contact));
    }

    // ---------------------------------------------------------------
    // پاس / نقد
    // ---------------------------------------------------------------

    [Fact]
    public async Task Clear_Received_MovesMoneyToBank_OnTheChosenDate()
    {
        var contact = await AddContactAsync();
        var bank = await _db.AddAccountAsync(TenantA, BankName);
        var created = await CreateAsync(NewRequest(contact, bankAccountId: bank));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(created.Id, TenantA, new DateOnly(2026, 4, 1), "TRK-1");

        await using var verify = _db.NewContext();
        var cheque = await verify.Cheques.SingleAsync();
        var clearing = await verify.Transactions.SingleAsync(t => t.Id == cheque.TransactionId);

        Assert.Equal(ChequeStatus.Cleared, cheque.Status);
        Assert.Equal(new DateOnly(2026, 4, 1), cheque.ClearedDate);
        Assert.Equal("TRK-1", cheque.BankTrackingNumber);
        Assert.Equal(new DateOnly(2026, 4, 1), clearing.Date);
        Assert.Equal(2, await verify.Transactions.CountAsync());

        Assert.Equal(0, await BalanceAsync(ReceivedInstrument));     // حساب چک بسته شد
        Assert.Equal(Amount, await BalanceAsync(BankName));          // پول در بانک
        Assert.Equal(-Amount, await ContactBalanceAsync(contact));   // طرف حساب فقط یک‌بار اثر گرفت
    }

    [Fact]
    public async Task Clear_WithoutDate_UsesTodayInIran_NotUtcDate()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(created.Id, TenantA, null, null);

        await using var verify = _db.NewContext();
        Assert.Equal(IranToday, (await verify.Cheques.SingleAsync()).ClearedDate);
    }

    [Fact]
    public async Task Clear_Issued_PaysFromBank()
    {
        var contact = await AddContactAsync("فروشنده");
        var bank = await _db.AddAccountAsync(TenantA, BankName);
        var created = await CreateAsync(NewRequest(contact, ChequeDirection.Issued, bankAccountId: bank));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(created.Id, TenantA, null, null);

        Assert.Equal(0, await BalanceAsync(IssuedInstrument));
        Assert.Equal(-Amount, await BalanceAsync(BankName));
        Assert.Equal(Amount, await ContactBalanceAsync(contact));
    }

    [Fact]
    public async Task Cash_PendingCheque_BehavesLikeClear()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsCashedAsync(created.Id, TenantA, new DateOnly(2026, 3, 15), null);

        await using var verify = _db.NewContext();
        var cheque = await verify.Cheques.SingleAsync();

        Assert.Equal(ChequeStatus.Cashed, cheque.Status);
        Assert.NotNull(cheque.TransactionId);
        Assert.Equal(0, await BalanceAsync(ReceivedInstrument));
    }

    [Fact]
    public async Task Clear_Twice_OrFromBounced_IsRejected()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(created.Id, TenantA, null, null);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsClearedAsync(created.Id, TenantA, null, null));

        var other = await CreateAsync(NewRequest(await AddContactAsync("دیگری"), number: "2222"));
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(other.Id, TenantA, "موجودی ناکافی", null);

        // چک برگشتی را مستقیم نمی‌شود پاس کرد؛ باید «ارائه مجدد» شود
        await using (var context = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsClearedAsync(other.Id, TenantA, null, null));
            Assert.Contains("ارائه مجدد", ex.Message);
        }
    }

    // ---------------------------------------------------------------
    // برگشت و ارائه‌ی مجدد
    // ---------------------------------------------------------------

    [Fact]
    public async Task Bounce_ReversesRegistration_AndRestoresContactBalance()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(contact));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(created.Id, TenantA, "موجودی ناکافی", new DateOnly(2026, 4, 2));

        await using var verify = _db.NewContext();
        var cheque = await verify.Cheques.SingleAsync();

        Assert.Equal(ChequeStatus.Bounced, cheque.Status);
        Assert.Equal(new DateOnly(2026, 4, 2), cheque.BouncedDate);
        Assert.Equal("موجودی ناکافی", cheque.BounceReason);
        Assert.Null(cheque.RegistrationTransactionId);

        Assert.Equal(2, await verify.Transactions.CountAsync());      // ثبت + معکوس
        Assert.Equal(0, await BalanceAsync(ReceivedInstrument));
        Assert.Equal(0, await ContactBalanceAsync(contact));          // طرف حساب دوباره بدهکار است
    }

    [Fact]
    public async Task Bounce_WithoutDate_UsesTodayInIran()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(created.Id, TenantA, null, null);

        await using var verify = _db.NewContext();
        Assert.Equal(IranToday, (await verify.Cheques.SingleAsync()).BouncedDate);
    }

    [Fact]
    public async Task FullCycle_Bounce_RePresent_Clear_EndsWithOneNetEffect()
    {
        var contact = await AddContactAsync();
        var bank = await _db.AddAccountAsync(TenantA, BankName);
        var created = await CreateAsync(NewRequest(contact, bankAccountId: bank));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(created.Id, TenantA, "برگشت", new DateOnly(2026, 4, 2));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsRePresentedAsync(created.Id, TenantA, new DateOnly(2026, 6, 1), "سررسید جدید");

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(created.Id, TenantA, new DateOnly(2026, 6, 2), null);

        await using var verify = _db.NewContext();
        var cheque = await verify.Cheques.SingleAsync();

        Assert.Equal(ChequeStatus.Cleared, cheque.Status);
        Assert.Equal(new DateOnly(2026, 6, 1), cheque.DueDate);
        Assert.Null(cheque.BouncedDate);
        Assert.Equal(4, await verify.Transactions.CountAsync());      // ثبت، معکوس، ثبت مجدد، پاس

        Assert.Equal(0, await BalanceAsync(ReceivedInstrument));
        Assert.Equal(Amount, await BalanceAsync(BankName));
        Assert.Equal(-Amount, await ContactBalanceAsync(contact));
    }

    [Fact]
    public async Task RePresent_RequiresBouncedOrReturned_AndValidDate()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsRePresentedAsync(created.Id, TenantA, new DateOnly(2026, 6, 1), null));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(created.Id, TenantA, null, null);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsRePresentedAsync(created.Id, TenantA, default, null));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsRePresentedAsync(created.Id, TenantA, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public async Task Return_And_Refer_AreOnlyForBouncedCheques_AndAddNoDocument()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsReturnedAsync(created.Id, TenantA, null));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(created.Id, TenantA, null, null);

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsReturnedAsync(created.Id, TenantA, null);

        await using var verify = _db.NewContext();
        Assert.Equal(ChequeStatus.Returned, (await verify.Cheques.SingleAsync()).Status);
        Assert.Equal(2, await verify.Transactions.CountAsync());      // فقط ثبت + معکوس
    }

    // ---------------------------------------------------------------
    // ابطال
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cancel_PendingCheque_ReversesRegistration()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(contact));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsCancelledAsync(created.Id, TenantA, "اشتباه ثبت شد");

        await using var verify = _db.NewContext();
        var cheque = await verify.Cheques.SingleAsync();

        Assert.Equal(ChequeStatus.Cancelled, cheque.Status);
        Assert.Null(cheque.RegistrationTransactionId);
        Assert.Equal(0, await BalanceAsync(ReceivedInstrument));
        Assert.Equal(0, await ContactBalanceAsync(contact));
    }

    [Fact]
    public async Task Cancel_AfterBounce_DoesNotCreateAnotherReversal()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(created.Id, TenantA, null, null);

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsCancelledAsync(created.Id, TenantA, null);

        await using var verify = _db.NewContext();
        Assert.Equal(2, await verify.Transactions.CountAsync());
        Assert.Equal(0, await BalanceAsync(ReceivedInstrument));
    }

    [Fact]
    public async Task Cancel_ClearedOrCashedOrAlreadyCancelled_IsRejected()
    {
        var cleared = await CreateAsync(NewRequest(await AddContactAsync(), number: "1"));
        var cashed = await CreateAsync(NewRequest(await AddContactAsync("ب"), number: "2"));
        var cancelled = await CreateAsync(NewRequest(await AddContactAsync("ج"), number: "3"));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(cleared.Id, TenantA, null, null);
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsCashedAsync(cashed.Id, TenantA, null, null);
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsCancelledAsync(cancelled.Id, TenantA, null);

        foreach (var id in new[] { cleared.Id, cashed.Id, cancelled.Id })
        {
            await using var context = _db.NewContext();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsCancelledAsync(id, TenantA, null));
        }
    }

    // ---------------------------------------------------------------
    // برگشت از پاس / نقد
    // ---------------------------------------------------------------

    [Fact]
    public async Task Unclear_RemovesClearingDocument_AndKeepsChequeRegistered()
    {
        var contact = await AddContactAsync();
        var bank = await _db.AddAccountAsync(TenantA, BankName);
        var created = await CreateAsync(NewRequest(contact, bankAccountId: bank));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(created.Id, TenantA, null, "TRK");

        await using (var context = _db.NewContext())
            await NewService(context).UnclearAsync(created.Id, TenantA, null);

        await using var verify = _db.NewContext();
        var cheque = await verify.Cheques.SingleAsync();

        Assert.Equal(ChequeStatus.InProgress, cheque.Status);
        Assert.Null(cheque.TransactionId);
        Assert.Null(cheque.ClearedDate);
        Assert.Null(cheque.BankTrackingNumber);
        Assert.NotNull(cheque.RegistrationTransactionId);

        Assert.Equal(1, await verify.Transactions.CountAsync());      // فقط سند ثبت
        Assert.Equal(Amount, await BalanceAsync(ReceivedInstrument));
        Assert.Equal(0, await BalanceAsync(BankName));
        Assert.Equal(-Amount, await ContactBalanceAsync(contact));
    }

    [Fact]
    public async Task Uncash_OnlyForCashedCheques()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).UncashAsync(created.Id, TenantA, null));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsCashedAsync(created.Id, TenantA, null, null);

        await using (var context = _db.NewContext())
            await NewService(context).UncashAsync(created.Id, TenantA, null);

        await using var verify = _db.NewContext();
        Assert.Equal(ChequeStatus.InProgress, (await verify.Cheques.SingleAsync()).Status);
    }

    // ---------------------------------------------------------------
    // چک قدیمی (قبل از این مدل): بدون سند ثبت
    // ---------------------------------------------------------------

    [Fact]
    public async Task LegacyChequeWithoutRegistration_IsRegisteredBeforeClearing()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(contact));

        // شبیه‌سازی چک قدیمی: سند ثبت و اشاره‌گرش وجود ندارد
        await using (var context = _db.NewContext())
        {
            var cheque = await context.Cheques.SingleAsync();
            await new TransactionService(context).DeleteAsync(cheque.RegistrationTransactionId!.Value, TenantA);
            cheque.RegistrationTransactionId = null;
            await context.SaveChangesAsync();
        }

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(created.Id, TenantA, null, null);

        await using var verify = _db.NewContext();
        var loaded = await verify.Cheques.SingleAsync();

        Assert.NotNull(loaded.RegistrationTransactionId);
        Assert.Equal(2, await verify.Transactions.CountAsync());      // ثبت (ترمیم‌شده) + پاس
        Assert.Equal(0, await BalanceAsync(ReceivedInstrument));
        Assert.Equal(-Amount, await ContactBalanceAsync(contact));
    }

    // ---------------------------------------------------------------
    // ویرایش
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_PendingCheque_RebuildsRegistrationWithNewAmount()
    {
        var contact = await AddContactAsync();
        var created = await CreateAsync(NewRequest(contact));
        var oldRegistrationId = (await LoadChequeAsync(created.Id)).RegistrationTransactionId;

        var edit = NewRequest(contact);
        edit.Amount = 2_000_000;
        edit.Description = "مبلغ اصلاح شد";

        await using (var context = _db.NewContext())
            await NewService(context).UpdateAsync(created.Id, edit, TenantA);

        var cheque = await LoadChequeAsync(created.Id);
        Assert.Equal(2_000_000, cheque.Amount);
        Assert.NotEqual(oldRegistrationId, cheque.RegistrationTransactionId);

        await using var verify = _db.NewContext();
        Assert.Equal(1, await verify.Transactions.CountAsync());      // سند قدیمی حذف نرم شده
        Assert.Equal(2_000_000, await BalanceAsync(ReceivedInstrument));
        Assert.Equal(-2_000_000, await ContactBalanceAsync(contact));

        // تاریخ ثبت قبلی حفظ شده
        Assert.Equal(new DateOnly(2026, 3, 10), (await verify.Transactions.SingleAsync()).Date);
    }

    [Fact]
    public async Task Update_RejectsDirectionChange_ClearedCheque_AndAmountChangeOnReturned()
    {
        var contact = await AddContactAsync();
        var pending = await CreateAsync(NewRequest(contact, number: "1"));

        var directionChange = NewRequest(contact, ChequeDirection.Issued, number: "1");
        await using (var context = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).UpdateAsync(pending.Id, directionChange, TenantA));
            Assert.Contains("جهت", ex.Message);
        }

        var cleared = await CreateAsync(NewRequest(contact, number: "2"));
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsClearedAsync(cleared.Id, TenantA, null, null);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).UpdateAsync(cleared.Id, NewRequest(contact, number: "2"), TenantA));

        var returned = await CreateAsync(NewRequest(contact, number: "3"));
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(returned.Id, TenantA, null, null);
        await using (var context = _db.NewContext())
            await NewService(context).MarkAsReturnedAsync(returned.Id, TenantA, null);

        // تغییر مبلغ چک عودت‌شده رد می‌شود؛ تغییر توضیحات مجاز است
        var amountChange = NewRequest(contact, number: "3");
        amountChange.Amount = 5;
        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).UpdateAsync(returned.Id, amountChange, TenantA));

        var descriptionChange = NewRequest(contact, number: "3");
        descriptionChange.Description = "توضیح جدید";
        await using (var context = _db.NewContext())
            await NewService(context).UpdateAsync(returned.Id, descriptionChange, TenantA);

        Assert.Equal("توضیح جدید", (await LoadChequeAsync(returned.Id)).Description);
    }

    // ---------------------------------------------------------------
    // حذف
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_InProgressCheque_RemovesItsDocuments_ButOthersCannotBeDeleted()
    {
        var contact = await AddContactAsync();
        var draft = await CreateAsync(NewRequest(contact, number: "1"));
        var bounced = await CreateAsync(NewRequest(contact, number: "2"));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(bounced.Id, TenantA, null, null);

        _db.User.UserId = "deleter";
        await using (var context = _db.NewContext())
            await NewService(context).DeleteAsync(draft.Id, TenantA);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).DeleteAsync(bounced.Id, TenantA));

        await using var verify = _db.NewContext();
        Assert.Single(await verify.Cheques.ToListAsync());

        var deleted = await verify.SetIncludingDeleted<Cheque>().SingleAsync(c => c.Id == draft.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal("deleter", deleted.DeletedBy);

        // سندهای چک حذف‌شده هم از دفتر رفته‌اند؛ فقط ثبت+معکوس چک برگشتی مانده
        Assert.Equal(2, await verify.Transactions.CountAsync());
    }

    // ---------------------------------------------------------------
    // سایر
    // ---------------------------------------------------------------

    [Fact]
    public async Task Deposit_OnlyAddsHistory_AndOnlyForPendingCheques()
    {
        var created = await CreateAsync(NewRequest(await AddContactAsync()));

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsDepositedAsync(created.Id, TenantA, null);

        await using (var verify = _db.NewContext())
        {
            Assert.Equal(ChequeStatus.InProgress, (await verify.Cheques.SingleAsync()).Status);
            Assert.Contains(await verify.ChequeHistories.ToListAsync(), h => h.Action == ChequeHistoryAction.Deposited);
            Assert.Equal(1, await verify.Transactions.CountAsync());
        }

        await using (var context = _db.NewContext())
            await NewService(context).MarkAsBouncedAsync(created.Id, TenantA, null, null);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).MarkAsDepositedAsync(created.Id, TenantA, null));
    }

    [Fact]
    public async Task Create_InvalidRequests_AreRejected_AndLeaveNothingBehind()
    {
        var contact = await AddContactAsync();

        var cases = new (Action<ChequeRequest> Mutate, string Expected)[]
        {
            (r => r.ChequeNumber = "  ", "شماره چک"),
            (r => r.Amount = 0, "مبلغ"),
            (r => r.Amount = 1_000.5m, "عدد صحیح"),
            (r => r.IssueDate = default, "تاریخ صدور"),
            (r => r.DueDate = default, "تاریخ سررسید"),
            (r => r.DueDate = new DateOnly(2026, 3, 1), "قبل از تاریخ صدور"),
            (r => r.ContactId = 999_999, "طرف حساب"),
            (r => r.BankAccountId = 999_999, "حساب بانکی"),
        };

        foreach (var (mutate, expected) in cases)
        {
            var request = NewRequest(contact);
            mutate(request);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(request));
            Assert.Contains(expected, ex.Message);
        }

        await using var verify = _db.NewContext();
        Assert.Empty(await verify.Cheques.ToListAsync());
        Assert.Empty(await verify.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Create_WithInvoiceOfWrongTypeOrOtherContact_IsRejected()
    {
        var contact = await AddContactAsync();
        var otherContact = await AddContactAsync("دیگری");

        int purchaseInvoice, saleInvoiceOfOther;
        await using (var context = _db.NewContext())
        {
            var purchase = new Invoice
            {
                Number = 1, InvoiceType = InvoiceType.Purchase, Status = InvoiceStatus.Posted,
                ContactId = contact, Date = new DateOnly(2026, 3, 1), TotalAmount = 100
            };
            var otherSale = new Invoice
            {
                Number = 1, InvoiceType = InvoiceType.Sale, Status = InvoiceStatus.Posted,
                ContactId = otherContact, Date = new DateOnly(2026, 3, 1), TotalAmount = 100
            };
            context.Invoices.AddRange(purchase, otherSale);
            await context.SaveChangesAsync();
            purchaseInvoice = purchase.Id;
            saleInvoiceOfOther = otherSale.Id;
        }

        var wrongType = NewRequest(contact);
        wrongType.InvoiceId = purchaseInvoice;   // چک دریافتنی باید به فاکتور «فروش» وصل شود
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(wrongType));

        var wrongContact = NewRequest(contact);
        wrongContact.InvoiceId = saleInvoiceOfOther;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(wrongContact));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private ChequeService NewService(ApplicationDbContext context)
        => new(context, new TransactionService(context), new SystemAccountService(context), _db.User, _time);

    private async Task<Cheque> CreateAsync(ChequeRequest request)
    {
        await using var context = _db.NewContext();
        return await NewService(context).CreateAsync(request, TenantA);
    }

    private async Task<Cheque> LoadChequeAsync(int id)
    {
        await using var context = _db.NewContext();
        return await context.Cheques.SingleAsync(c => c.Id == id);
    }

    private async Task<int> AddContactAsync(string name = "مشتری الف")
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = name, ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    /// <summary>مانده‌ی بدهکار−بستانکار یک حساب (بر اساس نام).</summary>
    private async Task<decimal> BalanceAsync(string accountName)
    {
        await using var context = _db.NewContext();
        var lines = await context.TransactionLines
            .Where(l => l.Account.Name == accountName)
            .ToListAsync();

        return lines.Sum(l => l.Debit - l.Credit);
    }

    /// <summary>مانده‌ی بدهکار−بستانکار روی خطوط یک طرف حساب (منفی = طلبکار/کاهش بدهی او).</summary>
    private async Task<decimal> ContactBalanceAsync(int contactId)
    {
        await using var context = _db.NewContext();
        var lines = await context.TransactionLines
            .Where(l => l.ContactId == contactId)
            .ToListAsync();

        return lines.Sum(l => l.Debit - l.Credit);
    }

    private static ChequeRequest NewRequest(
        int contactId,
        ChequeDirection direction = ChequeDirection.Received,
        int? bankAccountId = null,
        string number = "1001")
        => new()
        {
            ChequeNumber = number,
            Direction = direction,
            Amount = Amount,
            IssueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 5, 10),
            ContactId = contactId,
            BankAccountId = bankAccountId,
            DrawerName = "صاحب چک"
        };
}
