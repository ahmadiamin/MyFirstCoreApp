using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// تست پروسیجر داشبورد روی SQL Server واقعی. بدون متغیر محیطی MYFIRSTCOREAPP_TEST_SQLSERVER، Skip می‌شود.
/// چک‌ها و پرداخت‌ها عمداً مستقیم (بدون سرویس) ساخته می‌شوند تا مانده‌ی دفتر فقط از اسناد خود تست بیاید.
/// </summary>
public class DashboardServiceSqlServerTests : IDisposable
{
    private const int Tenant = 1;
    private const int OtherTenant = 2;

    private readonly SqlServerTestDatabase _db = new();

    // امروز (ایران) = 2026-07-01 ساعت 12:00
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.Zero));

    public DashboardServiceSqlServerTests()
    {
        _db.Tenant.TenantId = Tenant;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    [SqlServerFact]
    public async Task Summary_ComputesKpis_DueCheques_TopBalances_AndMonthlyFlow()
    {
        await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));   // 1405

        var bank = await _db.AddAccountAsync(Tenant, "بانک", AccountType.Bank);
        var receivable = await _db.AddAccountAsync(Tenant, "دریافتنی", AccountType.Receivable);
        var payable = await _db.AddAccountAsync(Tenant, "پرداختنی", AccountType.Payable);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);
        var purchases = await _db.AddAccountAsync(Tenant, "خرید", AccountType.Expense);

        var contactA = await AddContactAsync("الف");
        var contactB = await AddContactAsync("ب");

        // ---------- دفتر ----------
        await AddDocumentAsync(new DateOnly(2026, 5, 5), bank, sales, 600);                                  // بانک +600
        await AddDocumentAsync(new DateOnly(2026, 5, 1), receivable, sales, 1000, debitContactId: contactA);  // الف 1000 بدهکار
        await AddDocumentAsync(new DateOnly(2026, 5, 10), bank, receivable, 300, creditContactId: contactA);  // بانک +300، الف 300 کم
        await AddDocumentAsync(new DateOnly(2026, 6, 10), purchases, payable, 400, creditContactId: contactB); // ب 400 بستانکار

        // ---------- فاکتورها ----------
        await AddInvoiceAsync(1, InvoiceType.Sale, InvoiceStatus.Posted, contactA, new DateOnly(2026, 5, 1), total: 1000, paid: 300);   // باز: 700
        await AddInvoiceAsync(2, InvoiceType.Sale, InvoiceStatus.Posted, contactA, new DateOnly(2026, 6, 1), total: 500, paid: 500);    // بسته
        await AddInvoiceAsync(3, InvoiceType.Purchase, InvoiceStatus.Posted, contactB, new DateOnly(2026, 6, 10), total: 400);          // باز: 400
        await AddInvoiceAsync(4, InvoiceType.Sale, InvoiceStatus.Draft, contactA, new DateOnly(2026, 6, 12), total: 9_999);             // پیش‌نویس ← نباید بیاید
        await AddInvoiceAsync(5, InvoiceType.Sale, InvoiceStatus.Posted, contactA, new DateOnly(2025, 6, 1), total: 777);               // سال قبل: باز ولی خارج از فروش سال
        await AddInvoiceAsync(9, InvoiceType.Sale, InvoiceStatus.Posted, contactA, new DateOnly(2026, 6, 1), total: 5_000, tenant: OtherTenant);

        // ---------- پرداخت‌ها ----------
        await AddPaymentAsync(1, PaymentType.Received, PaymentStatus.Posted, contactA, new DateOnly(2026, 5, 10), 300);
        await AddPaymentAsync(2, PaymentType.Paid, PaymentStatus.Posted, contactB, new DateOnly(2026, 6, 15), 100);
        await AddPaymentAsync(3, PaymentType.Received, PaymentStatus.Draft, contactA, new DateOnly(2026, 6, 16), 9_999);   // پیش‌نویس

        // ---------- چک‌ها ----------
        await AddChequeAsync("R1", ChequeDirection.Received, ChequeStatus.InProgress, contactA, new DateOnly(2026, 6, 25), 50);     // 6 روز گذشته
        await AddChequeAsync("R2", ChequeDirection.Received, ChequeStatus.RePresented, contactA, new DateOnly(2026, 7, 5), 60);     // 4 روز مانده
        await AddChequeAsync("R3", ChequeDirection.Received, ChequeStatus.InProgress, contactA, new DateOnly(2026, 8, 30), 70);     // خارج از پنجره‌ی 7 روز
        await AddChequeAsync("R4", ChequeDirection.Received, ChequeStatus.Cleared, contactA, new DateOnly(2026, 6, 20), 999);       // پاس‌شده ← نباید بیاید
        await AddChequeAsync("I1", ChequeDirection.Issued, ChequeStatus.InProgress, contactB, new DateOnly(2026, 7, 1), 80);        // امروز

        var summary = await NewService().GetSummaryAsync(dueWindowDays: 7, topN: 5);

        // بازه و تاریخ
        Assert.Equal(new DateOnly(2026, 3, 21), summary.FiscalFrom);
        Assert.Equal(new DateOnly(2027, 3, 20), summary.FiscalTo);
        Assert.Equal(new DateOnly(2026, 7, 1), summary.Today);
        Assert.Equal(new DateOnly(2026, 7, 8), summary.DueUntil);

        // فروش/خرید قطعیِ سال مالی
        Assert.Equal(1500, summary.SalesTotal);
        Assert.Equal(400, summary.PurchasesTotal);

        // فاکتورهای باز: شامل سال قبل، بدون پیش‌نویس، بدون شرکت دیگر
        Assert.Equal(2, summary.OpenSaleCount);
        Assert.Equal(700 + 777, summary.OpenSaleRemaining);
        Assert.Equal(1, summary.OpenPurchaseCount);
        Assert.Equal(400, summary.OpenPurchaseRemaining);

        // مانده‌ها
        Assert.Equal(900, summary.CashAndBankBalance);
        Assert.Equal(700, summary.ReceivableBalance);
        Assert.Equal(400, summary.PayableBalance);

        // چک‌های در انتظار
        Assert.Equal(3, summary.PendingReceivedChequeCount);
        Assert.Equal(50 + 60 + 70, summary.PendingReceivedChequeAmount);
        Assert.Equal(1, summary.PendingIssuedChequeCount);
        Assert.Equal(80, summary.PendingIssuedChequeAmount);

        // چک‌های سررسید (تا 7 روز بعد)، به ترتیب سررسید
        Assert.Equal(new[] { "R1", "R2" }, summary.DueReceivedCheques.Select(c => c.ChequeNumber).ToArray());
        Assert.Equal(new[] { -6, 4 }, summary.DueReceivedCheques.Select(c => c.DaysLeft).ToArray());
        Assert.True(summary.DueReceivedCheques[0].IsOverdue);

        var issued = Assert.Single(summary.DueIssuedCheques);
        Assert.Equal("I1", issued.ChequeNumber);
        Assert.Equal(0, issued.DaysLeft);
        Assert.Equal("ب", issued.ContactName);

        // بدهکار و بستانکار برتر
        var debtor = Assert.Single(summary.TopDebtors);
        Assert.Equal("الف", debtor.Name);
        Assert.Equal(700, debtor.Balance);

        var creditor = Assert.Single(summary.TopCreditors);
        Assert.Equal("ب", creditor.Name);
        Assert.Equal(-400, creditor.Balance);

        // جریان ماهانه
        Assert.Equal(12, summary.Months.Count);

        var ordibehesht = summary.Months[1];   // 04-21 تا 05-21
        Assert.Equal(1000, ordibehesht.Sales);
        Assert.Equal(300, ordibehesht.Received);

        var khordad = summary.Months[2];       // 05-22 تا 06-21
        Assert.Equal(500, khordad.Sales);
        Assert.Equal(400, khordad.Purchases);
        Assert.Equal(100, khordad.Paid);
        Assert.Equal(0, khordad.Received);      // پرداخت پیش‌نویس نیامده

        Assert.Equal(1000, summary.MaxMonthlyValue);
    }

    [SqlServerFact]
    public async Task Summary_ForATenantWithoutData_IsAllZeros_AndStillHasTwelveMonths()
    {
        var summary = await NewService().GetSummaryAsync();

        Assert.Equal(0, summary.SalesTotal);
        Assert.Equal(0, summary.CashAndBankBalance);
        Assert.Empty(summary.DueReceivedCheques);
        Assert.Empty(summary.TopDebtors);
        Assert.Equal(12, summary.Months.Count);     // سال شمسی جاری (سالی تعریف نشده)
    }

    [SqlServerFact]
    public async Task Summary_RequiresAnActiveTenant()
    {
        _db.Tenant.TenantId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().GetSummaryAsync());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private DashboardService NewService()
    {
        var context = _db.NewContext();
        return new DashboardService(context, _db.Tenant, new FiscalYearService(context, _db.User, _time), _time);
    }

    private async Task CreateYearAsync(DateOnly start, DateOnly end)
    {
        await using var context = _db.NewContext();
        await new FiscalYearService(context, _db.User, _time)
            .CreateAsync(new FiscalYearRequest { StartDate = start, EndDate = end }, Tenant);
    }

    private async Task<int> AddContactAsync(string name)
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = name, ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private async Task AddDocumentAsync(
        DateOnly date, int debitAccount, int creditAccount, decimal amount,
        int? debitContactId = null, int? creditContactId = null)
    {
        await using var context = _db.NewContext();
        var service = new TransactionService(context);

        var created = await service.CreateAsync(new Transaction
        {
            TenantId = Tenant,
            Date = date,
            Lines =
            {
                new TransactionLine { AccountId = debitAccount, ContactId = debitContactId, Debit = amount },
                new TransactionLine { AccountId = creditAccount, ContactId = creditContactId, Credit = amount }
            }
        });

        await service.PostAsync(created.Id, Tenant);
    }

    private async Task AddInvoiceAsync(
        int number, InvoiceType type, InvoiceStatus status, int contactId, DateOnly date,
        decimal total, decimal paid = 0, int tenant = Tenant)
    {
        await WithTenantAsync(tenant, async context =>
        {
            context.Invoices.Add(new Invoice
            {
                Number = number,
                InvoiceType = type,
                Status = status,
                ContactId = contactId,
                Date = date,
                TotalAmount = total,
                PaidAmount = paid
            });
            await context.SaveChangesAsync();
        });
    }

    private async Task AddPaymentAsync(
        int number, PaymentType type, PaymentStatus status, int contactId, DateOnly date, decimal amount)
    {
        await WithTenantAsync(Tenant, async context =>
        {
            context.Payments.Add(new Payment
            {
                Number = number,
                PaymentType = type,
                Status = status,
                ContactId = contactId,
                Date = date,
                Amount = amount,
                PaymentMethod = PaymentMethod.Cash
            });
            await context.SaveChangesAsync();
        });
    }

    private async Task AddChequeAsync(
        string number, ChequeDirection direction, ChequeStatus status, int contactId, DateOnly dueDate, decimal amount)
    {
        await WithTenantAsync(Tenant, async context =>
        {
            context.Cheques.Add(new Cheque
            {
                ChequeNumber = number,
                Direction = direction,
                Status = status,
                ContactId = contactId,
                IssueDate = new DateOnly(2026, 3, 25),
                DueDate = dueDate,
                Amount = amount
            });
            await context.SaveChangesAsync();
        });
    }

    private async Task WithTenantAsync(int tenant, Func<MyFirstCoreApp.Infrastructure.Persistence.ApplicationDbContext, Task> action)
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenant;

        try
        {
            await using var context = _db.NewContext();
            await action(context);
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }
}
