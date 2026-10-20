using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Banking;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// پرداخت با روش «چک»: سند حسابداری را خودِ چک می‌سازد و پرداخت فقط تخصیص به فاکتور را انجام می‌دهد.
/// مهم‌ترین تضمین: دریافتی هرگز دو بار در دفتر نمی‌نشیند.
/// </summary>
public class PaymentChequeTests : IDisposable
{
    private const int TenantA = 1;
    private const decimal ChequeAmount = 600_000;

    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

    public PaymentChequeTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // اعتبارسنجی
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChequeMethod_RequiresAChequeAndForbidsBankAccount()
    {
        var f = await ArrangeAsync();

        var withoutCheque = NewPayment(f, chequeId: null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePaymentAsync(withoutCheque));
        Assert.Contains("چک", ex.Message);

        // حساب بانکی برای روش چک نادیده گرفته (null) می‌شود
        var withBank = await CreatePaymentAsync(NewPayment(f, f.Cheque));
        Assert.Null(withBank.BankAccountId);
    }

    [Fact]
    public async Task ChequeId_WithNonChequeMethod_IsRejected()
    {
        var f = await ArrangeAsync();
        var payment = NewPayment(f, f.Cheque);
        payment.PaymentMethod = PaymentMethod.BankTransfer;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePaymentAsync(payment));
        Assert.Contains("روش", ex.Message);
    }

    [Fact]
    public async Task Cheque_MustMatchDirectionContactAmountAndStatus()
    {
        var f = await ArrangeAsync();

        // مبلغ نابرابر
        var wrongAmount = NewPayment(f, f.Cheque);
        wrongAmount.Amount = ChequeAmount - 1;
        wrongAmount.Allocations.Clear();
        wrongAmount.Allocations.Add(new PaymentAllocation { InvoiceId = f.Invoice, Amount = ChequeAmount - 1 });
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePaymentAsync(wrongAmount));
        Assert.Contains("برابر مبلغ چک", ex1.Message);

        // چک پرداختنی برای پرداخت از نوع «دریافت»
        var issued = await CreateChequeAsync(f.Contact, ChequeDirection.Issued, "9001");
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePaymentAsync(NewPayment(f, issued)));
        Assert.Contains("جهت", ex2.Message);

        // چک طرف حساب دیگر
        var otherContact = await AddContactAsync("مشتری دیگر");
        var othersCheque = await CreateChequeAsync(otherContact, ChequeDirection.Received, "9002");
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePaymentAsync(NewPayment(f, othersCheque)));
        Assert.Contains("طرف حساب", ex3.Message);

        // چک برگشتی قابل استفاده نیست
        await using (var context = _db.NewContext())
            await NewChequeService(context).MarkAsBouncedAsync(f.Cheque, TenantA, null, null);
        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePaymentAsync(NewPayment(f, f.Cheque)));
        Assert.Contains("چک", ex4.Message);
    }

    [Fact]
    public async Task OneChequeCanOnlyBackOnePayment()
    {
        var f = await ArrangeAsync();

        await CreatePaymentAsync(NewPayment(f, f.Cheque));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePaymentAsync(NewPayment(f, f.Cheque)));
        Assert.Contains("قبلاً", ex.Message);
    }

    // ---------------------------------------------------------------
    // قطعی / برگشت: بدون سند دوم
    // ---------------------------------------------------------------

    [Fact]
    public async Task Post_AppliesAllocations_ButCreatesNoSecondDocument_AndContactIsCreditedOnlyOnce()
    {
        var f = await ArrangeAsync();
        var payment = await CreatePaymentAsync(NewPayment(f, f.Cheque));

        await PostPaymentAsync(payment.Id);

        await using var verify = _db.NewContext();

        var loaded = await verify.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Posted, loaded.Status);
        Assert.Null(loaded.TransactionId);

        Assert.Equal(ChequeAmount, (await verify.Invoices.SingleAsync(i => i.Id == f.Invoice)).PaidAmount);

        // فقط سند «ثبت چک» هست (پرداخت سند نساخته)
        var documents = await verify.Transactions.ToListAsync();
        Assert.Single(documents);
        Assert.Equal("Cheque", documents[0].ReferenceType);

        // طرف حساب فقط یک‌بار (به‌خاطر ثبت چک) بستانکار شده؛ اگر پرداخت هم سند می‌ساخت 2 برابر می‌شد
        var contactLines = await verify.TransactionLines.Where(l => l.ContactId == f.Contact).ToListAsync();
        Assert.Equal(-ChequeAmount, contactLines.Sum(l => l.Debit - l.Credit));
    }

    [Fact]
    public async Task Unpost_RevertsAllocations_AndLeavesTheChequeDocumentsUntouched()
    {
        var f = await ArrangeAsync();
        var payment = await CreatePaymentAsync(NewPayment(f, f.Cheque));
        await PostPaymentAsync(payment.Id);

        await using (var context = _db.NewContext())
            await NewPaymentService(context).UnpostAsync(payment.Id, TenantA);

        await using var verify = _db.NewContext();

        Assert.Equal(PaymentStatus.Draft, (await verify.Payments.SingleAsync()).Status);
        Assert.Equal(0, (await verify.Invoices.SingleAsync(i => i.Id == f.Invoice)).PaidAmount);
        Assert.Single(await verify.Transactions.ToListAsync());   // سند ثبت چک سر جایش است
    }

    [Fact]
    public async Task Post_FailsIfTheChequeBouncedAfterTheDraftWasCreated()
    {
        var f = await ArrangeAsync();
        var payment = await CreatePaymentAsync(NewPayment(f, f.Cheque));

        await using (var context = _db.NewContext())
            await NewChequeService(context).MarkAsBouncedAsync(f.Cheque, TenantA, "برگشت", null);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => NewPaymentService(context).PostAsync(payment.Id, TenantA));

        await using var verify = _db.NewContext();
        Assert.Equal(0, (await verify.Invoices.SingleAsync(i => i.Id == f.Invoice)).PaidAmount);
    }

    // ---------------------------------------------------------------
    // محافظت‌های سمت چک
    // ---------------------------------------------------------------

    [Fact]
    public async Task Cheque_CannotBounceOrCancel_WhileAPostedPaymentRelies_ButCanAfterUnposting()
    {
        var f = await ArrangeAsync();
        var payment = await CreatePaymentAsync(NewPayment(f, f.Cheque));
        await PostPaymentAsync(payment.Id);

        await using (var context = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewChequeService(context).MarkAsBouncedAsync(f.Cheque, TenantA, null, null));
            Assert.Contains("پرداخت شماره", ex.Message);
        }

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewChequeService(context).MarkAsCancelledAsync(f.Cheque, TenantA, null));

        await using (var context = _db.NewContext())
            await NewPaymentService(context).UnpostAsync(payment.Id, TenantA);

        await using (var context = _db.NewContext())
            await NewChequeService(context).MarkAsBouncedAsync(f.Cheque, TenantA, "برگشت", null);

        await using var verify = _db.NewContext();
        Assert.Equal(ChequeStatus.Bounced, (await verify.Cheques.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cheque_AmountAndContactAreLocked_WhileAPostedPaymentRelies()
    {
        var f = await ArrangeAsync();
        var payment = await CreatePaymentAsync(NewPayment(f, f.Cheque));
        await PostPaymentAsync(payment.Id);

        var edit = NewChequeRequest(f.Contact, "1001");
        edit.Amount = ChequeAmount + 1;

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewChequeService(context).UpdateAsync(f.Cheque, edit, TenantA));

        // تغییر توضیحات مجاز است
        var descriptionOnly = NewChequeRequest(f.Contact, "1001");
        descriptionOnly.Description = "توضیح جدید";

        await using (var context = _db.NewContext())
            await NewChequeService(context).UpdateAsync(f.Cheque, descriptionOnly, TenantA);

        await using var verify = _db.NewContext();
        Assert.Equal("توضیح جدید", (await verify.Cheques.SingleAsync()).Description);
    }

    [Fact]
    public async Task Cheque_CannotBeDeleted_WhileAnyPaymentUsesIt()
    {
        var f = await ArrangeAsync();
        var draftPayment = await CreatePaymentAsync(NewPayment(f, f.Cheque));

        await using (var context = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewChequeService(context).DeleteAsync(f.Cheque, TenantA));
            Assert.Contains("پرداخت شماره", ex.Message);
        }

        // بعد از حذف پیش‌نویس پرداخت، حذف چک ممکن است
        await using (var context = _db.NewContext())
            await NewPaymentService(context).DeleteAsync(draftPayment.Id, TenantA);

        await using (var context = _db.NewContext())
            await NewChequeService(context).DeleteAsync(f.Cheque, TenantA);

        await using var verify = _db.NewContext();
        Assert.Empty(await verify.Cheques.ToListAsync());
    }

    // ---------------------------------------------------------------
    // Arrange / Helpers
    // ---------------------------------------------------------------

    private sealed record Fixture(int Contact, int Invoice, int Cheque);

    private async Task<Fixture> ArrangeAsync()
    {
        var contact = await AddContactAsync();

        int invoiceId;
        await using (var context = _db.NewContext())
        {
            var invoice = new Invoice
            {
                Number = 1,
                InvoiceType = InvoiceType.Sale,
                Status = InvoiceStatus.Posted,
                ContactId = contact,
                Date = new DateOnly(2026, 3, 1),
                TotalAmount = 1_000_000
            };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        var cheque = await CreateChequeAsync(contact, ChequeDirection.Received, "1001");

        return new Fixture(contact, invoiceId, cheque);
    }

    private Payment NewPayment(Fixture f, int? chequeId)
        => new()
        {
            TenantId = TenantA,
            PaymentType = PaymentType.Received,
            PaymentMethod = PaymentMethod.Cheque,
            Date = new DateOnly(2026, 3, 22),
            ContactId = f.Contact,
            BankAccountId = 12345,            // باید نادیده گرفته شود
            ChequeId = chequeId,
            Amount = ChequeAmount,
            Allocations = { new PaymentAllocation { InvoiceId = f.Invoice, Amount = ChequeAmount } }
        };

    private ChequeRequest NewChequeRequest(int contactId, string number)
        => new()
        {
            ChequeNumber = number,
            Direction = ChequeDirection.Received,
            Amount = ChequeAmount,
            IssueDate = new DateOnly(2026, 3, 10),
            DueDate = new DateOnly(2026, 5, 10),
            ContactId = contactId
        };

    private async Task<int> CreateChequeAsync(int contactId, ChequeDirection direction, string number)
    {
        var request = NewChequeRequest(contactId, number);
        request.Direction = direction;

        await using var context = _db.NewContext();
        return (await NewChequeService(context).CreateAsync(request, TenantA)).Id;
    }

    private async Task<Payment> CreatePaymentAsync(Payment payment)
    {
        await using var context = _db.NewContext();
        return await NewPaymentService(context).CreateAsync(payment);
    }

    private async Task PostPaymentAsync(int id)
    {
        await using var context = _db.NewContext();
        await NewPaymentService(context).PostAsync(id, TenantA);
    }

    private async Task<int> AddContactAsync(string name = "مشتری الف")
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = name, ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private PaymentService NewPaymentService(ApplicationDbContext context)
        => new(context, new TransactionService(context), new SystemAccountService(context), _db.User);

    private ChequeService NewChequeService(ApplicationDbContext context)
        => new(context, new TransactionService(context), new SystemAccountService(context), _db.User, _time);
}
