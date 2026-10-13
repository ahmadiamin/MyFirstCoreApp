using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class PaymentServiceTests : IDisposable
{
    private const int TenantA = 1;

    private readonly TestDatabase _db = new();

    public PaymentServiceTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // ثبت
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ValidPayment_SavesDraftWithNumberAndHistory()
    {
        var f = await ArrangeAsync();

        var created = await CreateAsync(NewPayment(f, amount: 1_000, allocate: (f.Invoice1, 600)));

        Assert.Equal(1, created.Number);
        Assert.Equal(PaymentStatus.Draft, created.Status);
        Assert.Equal(600, created.AllocatedAmount);

        await using var verify = _db.NewContext();
        var history = await verify.PaymentHistories.SingleAsync();
        Assert.Equal(PaymentStatus.Draft, history.ToStatus);
        Assert.Equal("user-1", history.UserId);
    }

    [Fact]
    public async Task Create_AllocationExceedsPaymentAmount_Throws()
    {
        var f = await ArrangeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAsync(NewPayment(f, amount: 500, allocate: (f.Invoice1, 600))));
    }

    [Fact]
    public async Task Create_AllocationExceedsInvoiceRemaining_ConsidersSettlementDiscount()
    {
        // فاکتور ۱٬۰۰۰ با تخفیف تسویه‌ی ۲۰۰ ← باقی‌مانده‌ی واقعی ۸۰۰
        var f = await ArrangeAsync(invoice1Discount: 200);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAsync(NewPayment(f, amount: 900, allocate: (f.Invoice1, 900))));

        Assert.Contains("باقی‌مانده", ex.Message);

        // تا سقف ۸۰۰ مجاز است
        var ok = await CreateAsync(NewPayment(f, amount: 800, allocate: (f.Invoice1, 800)));
        Assert.Equal(800, ok.AllocatedAmount);
    }

    [Fact]
    public async Task Create_InvoiceOfAnotherContact_Throws()
    {
        var f = await ArrangeAsync();
        var payment = NewPayment(f, amount: 500, allocate: (f.InvoiceOfOtherContact, 500));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(payment));
        Assert.Contains("متعلق", ex.Message);
    }

    [Fact]
    public async Task Create_ReceivedPaymentForPurchaseInvoice_Throws()
    {
        var f = await ArrangeAsync();
        var payment = NewPayment(f, amount: 500, allocate: (f.PurchaseInvoice, 500));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(payment));
        Assert.Contains("سازگار", ex.Message);
    }

    [Fact]
    public async Task Create_SameInvoiceAllocatedTwice_Throws()
    {
        var f = await ArrangeAsync();
        var payment = NewPayment(f, amount: 800, allocate: (f.Invoice1, 300));
        payment.Allocations.Add(new PaymentAllocation { InvoiceId = f.Invoice1, Amount = 300 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(payment));
        Assert.Contains("یک‌بار", ex.Message);
    }

    [Fact]
    public async Task Create_WithoutDate_Throws()
    {
        var f = await ArrangeAsync();
        var payment = NewPayment(f, amount: 500, allocate: (f.Invoice1, 500));
        payment.Date = default;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(payment));
        Assert.Contains("تاریخ", ex.Message);
    }

    [Fact]
    public async Task Create_WithNonBankAccountAsBank_Throws()
    {
        var f = await ArrangeAsync();
        var payment = NewPayment(f, amount: 500, allocate: (f.Invoice1, 500));
        payment.BankAccountId = 999_999;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(payment));
    }

    // ---------------------------------------------------------------
    // قطعی / برگشت
    // ---------------------------------------------------------------

    [Fact]
    public async Task Post_UpdatesInvoiceAndCreatesPostedDocument()
    {
        var f = await ArrangeAsync();
        var created = await CreateAsync(NewPayment(f, amount: 1_000, allocate: (f.Invoice1, 600)));

        await PostAsync(created.Id);

        await using var verify = _db.NewContext();

        var payment = await verify.Payments.SingleAsync(p => p.Id == created.Id);
        Assert.Equal(PaymentStatus.Posted, payment.Status);
        Assert.NotNull(payment.TransactionId);

        var invoice = await verify.Invoices.SingleAsync(i => i.Id == f.Invoice1);
        Assert.Equal(600, invoice.PaidAmount);

        var document = await verify.Transactions
            .Include(t => t.Lines)
            .SingleAsync(t => t.Id == payment.TransactionId);

        Assert.True(document.IsPosted);
        Assert.Equal("Payment", document.ReferenceType);
        Assert.Equal(payment.Id, document.ReferenceId);
        Assert.Equal(1_000, document.TotalAmount);
        Assert.Equal(payment.Date, document.Date);

        // دریافت: بانک بدهکار، دریافتنی (با طرف حساب) بستانکار
        var debitLine = document.Lines.Single(l => l.Debit > 0);
        var creditLine = document.Lines.Single(l => l.Credit > 0);
        Assert.Equal(f.BankAccount, debitLine.AccountId);
        Assert.Equal(f.Contact, creditLine.ContactId);
    }

    [Fact]
    public async Task Post_Twice_Throws()
    {
        var f = await ArrangeAsync();
        var created = await CreateAsync(NewPayment(f, amount: 500, allocate: (f.Invoice1, 500)));
        await PostAsync(created.Id);

        await using var context = _db.NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(context).PostAsync(created.Id, TenantA));
    }

    [Fact]
    public async Task Post_WhenInvoiceBecameFullyPaidMeanwhile_FailsAndChangesNothing()
    {
        var f = await ArrangeAsync();
        var created = await CreateAsync(NewPayment(f, amount: 600, allocate: (f.Invoice1, 600)));

        // فاکتور در فاصله‌ی ثبت پیش‌نویس و قطعی شدن، از مسیر دیگری تسویه می‌شود
        await using (var context = _db.NewContext())
        {
            var invoice = await context.Invoices.SingleAsync(i => i.Id == f.Invoice1);
            invoice.PaidAmount = invoice.TotalAmount;
            await context.SaveChangesAsync();
        }

        await using (var context = _db.NewContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).PostAsync(created.Id, TenantA));
        }

        await using var verify = _db.NewContext();
        Assert.Equal(PaymentStatus.Draft, (await verify.Payments.SingleAsync()).Status);
        Assert.Equal(1_000, (await verify.Invoices.SingleAsync(i => i.Id == f.Invoice1)).PaidAmount);
        Assert.Empty(await verify.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Unpost_RevertsInvoiceAndSoftDeletesDocument()
    {
        var f = await ArrangeAsync();
        var created = await CreateAsync(NewPayment(f, amount: 600, allocate: (f.Invoice1, 600)));
        await PostAsync(created.Id);

        await using (var context = _db.NewContext())
            await NewService(context).UnpostAsync(created.Id, TenantA);

        await using var verify = _db.NewContext();

        var payment = await verify.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Draft, payment.Status);
        Assert.Null(payment.TransactionId);

        Assert.Equal(0, (await verify.Invoices.SingleAsync(i => i.Id == f.Invoice1)).PaidAmount);

        Assert.Empty(await verify.Transactions.ToListAsync());
        var deleted = await verify.SetIncludingDeleted<Transaction>().SingleAsync();
        Assert.True(deleted.IsDeleted);
    }

    // ---------------------------------------------------------------
    // ویرایش / حذف
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_Draft_ReplacesAllocations()
    {
        var f = await ArrangeAsync();
        var created = await CreateAsync(NewPayment(f, amount: 1_000, allocate: (f.Invoice1, 400)));

        var edited = NewPayment(f, amount: 900, allocate: (f.Invoice2, 900));
        edited.Id = created.Id;

        await using (var context = _db.NewContext())
            await NewService(context).UpdateAsync(edited);

        await using var verify = _db.NewContext();
        var loaded = await verify.Payments.Include(p => p.Allocations).SingleAsync();

        Assert.Equal(900, loaded.Amount);
        Assert.Equal(900, loaded.AllocatedAmount);
        Assert.Single(loaded.Allocations);
        Assert.Equal(f.Invoice2, loaded.Allocations.Single().InvoiceId);
    }

    [Fact]
    public async Task Update_PostedPayment_Throws()
    {
        var f = await ArrangeAsync();
        var created = await CreateAsync(NewPayment(f, amount: 500, allocate: (f.Invoice1, 500)));
        await PostAsync(created.Id);

        var edited = NewPayment(f, amount: 100, allocate: (f.Invoice1, 100));
        edited.Id = created.Id;

        await using var context = _db.NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).UpdateAsync(edited));
    }

    [Fact]
    public async Task Delete_Draft_IsSoftDeleted_ButPostedCannotBeDeleted()
    {
        var f = await ArrangeAsync();
        var draft = await CreateAsync(NewPayment(f, amount: 300, allocate: (f.Invoice1, 300)));
        var posted = await CreateAsync(NewPayment(f, amount: 400, allocate: (f.Invoice2, 400)));
        await PostAsync(posted.Id);

        _db.User.UserId = "deleter";

        await using (var context = _db.NewContext())
            await NewService(context).DeleteAsync(draft.Id, TenantA);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => NewService(context).DeleteAsync(posted.Id, TenantA));

        await using var verify = _db.NewContext();
        Assert.Single(await verify.Payments.ToListAsync());

        var deleted = await verify.SetIncludingDeleted<Payment>().SingleAsync(p => p.Id == draft.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal("deleter", deleted.DeletedBy);
    }

    // ---------------------------------------------------------------
    // Arrange / Helpers
    // ---------------------------------------------------------------

    private sealed record Fixture(
        int Contact,
        int OtherContact,
        int BankAccount,
        int Invoice1,
        int Invoice2,
        int InvoiceOfOtherContact,
        int PurchaseInvoice);

    private async Task<Fixture> ArrangeAsync(decimal invoice1Discount = 0)
    {
        var bank = await _db.AddAccountAsync(TenantA, "بانک تست");

        await using var context = _db.NewContext();

        var customer = new Contact { Name = "مشتری الف", ContactType = ContactType.Customer };
        var otherCustomer = new Contact { Name = "مشتری ب", ContactType = ContactType.Customer };
        context.Contacts.AddRange(customer, otherCustomer);
        await context.SaveChangesAsync();

        var invoice1 = NewInvoice(1, InvoiceType.Sale, customer.Id, 1_000, invoice1Discount);
        var invoice2 = NewInvoice(2, InvoiceType.Sale, customer.Id, 1_000);
        var otherInvoice = NewInvoice(3, InvoiceType.Sale, otherCustomer.Id, 1_000);
        var purchase = NewInvoice(4, InvoiceType.Purchase, customer.Id, 1_000);
        context.Invoices.AddRange(invoice1, invoice2, otherInvoice, purchase);
        await context.SaveChangesAsync();

        return new Fixture(customer.Id, otherCustomer.Id, bank, invoice1.Id, invoice2.Id, otherInvoice.Id, purchase.Id);
    }

    private static Invoice NewInvoice(int number, InvoiceType type, int contactId, decimal total, decimal discount = 0)
    {
        return new Invoice
        {
            Number = number,
            InvoiceType = type,
            Status = InvoiceStatus.Posted,
            ContactId = contactId,
            Date = new DateOnly(DateTime.Now.Year,DateTime.Now.Month,DateTime.Now.Day),
            TotalAmount = total,
            PaidAmount = 0,
            SettlementDiscount = discount
        };
    }

    private static Payment NewPayment(Fixture f, decimal amount, (int InvoiceId, decimal Amount) allocate)
    {
        return new Payment
        {
            TenantId = TenantA,
            PaymentType = PaymentType.Received,
            Date = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day),
            ContactId = f.Contact,
            BankAccountId = f.BankAccount,
            PaymentMethod = PaymentMethod.BankTransfer,
            Amount = amount,
            Allocations =
            {
                new PaymentAllocation { InvoiceId = allocate.InvoiceId, Amount = allocate.Amount }
            }
        };
    }

    private PaymentService NewService(ApplicationDbContext context)
        => new(context, new TransactionService(context), new SystemAccountService(context), _db.User);

    private async Task<Payment> CreateAsync(Payment payment)
    {
        await using var context = _db.NewContext();
        return await NewService(context).CreateAsync(payment);
    }

    private async Task PostAsync(int paymentId)
    {
        await using var context = _db.NewContext();
        await NewService(context).PostAsync(paymentId, TenantA);
    }
}
