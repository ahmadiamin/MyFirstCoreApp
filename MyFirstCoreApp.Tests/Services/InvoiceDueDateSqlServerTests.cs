using MyFirstCoreApp.Application.Reports;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// «معوق» در گزارش فاکتورها، داشبورد و گزارش بدهکاران (روی SQL Server واقعی).
/// بدون متغیر محیطی MYFIRSTCOREAPP_TEST_SQLSERVER، Skip می‌شود.
///
/// معوق = فاکتور قطعی + باقی‌مانده‌ی مثبت + سررسیدِ داده‌شده و قبل از «امروز».
/// امروز = 2026-07-01.
/// </summary>
public class InvoiceDueDateSqlServerTests : IDisposable
{
    private const int Tenant = 1;
    private static readonly DateOnly Today = new(2026, 7, 1);

    private readonly SqlServerTestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.Zero));

    public InvoiceDueDateSqlServerTests()
    {
        _db.Tenant.TenantId = Tenant;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // گزارش فاکتورها
    // ---------------------------------------------------------------

    [SqlServerFact]
    public async Task InvoiceReport_FlagsAndFiltersOverdueInvoices_AndSumsTheirRemaining()
    {
        var contact = await AddContactAsync("الف");
        await SeedInvoicesAsync(contact);

        var all = await NewReportService().GetInvoicesAsync(new InvoiceReportQuery { Today = Today, PageSize = 100 });

        // تنها دو فاکتور معوق‌اند: فروش 1 (باقی‌مانده 1000) و خرید 1 (باقی‌مانده 700)
        var overdue = all.Rows.Where(r => r.IsOverdue).Select(r => (r.InvoiceType, r.Number)).OrderBy(x => x).ToList();
        Assert.Equal(
            new[] { (InvoiceType.Sale, 1), (InvoiceType.Purchase, 1) }.OrderBy(x => x).ToList(),
            overdue);

        Assert.Equal(2, all.OverdueCount);
        Assert.Equal(1_700, all.TotalOverdueRemaining);

        // سررسیدِ بدون مقدار، امروز و آینده: معوق نیست
        Assert.False(all.Rows.Single(r => r.InvoiceType == InvoiceType.Sale && r.Number == 4).IsOverdue);   // بدون سررسید
        Assert.False(all.Rows.Single(r => r.InvoiceType == InvoiceType.Purchase && r.Number == 2).IsOverdue); // سررسید = امروز
        Assert.False(all.Rows.Single(r => r.InvoiceType == InvoiceType.Sale && r.Number == 3).IsOverdue);   // آینده
        Assert.False(all.Rows.Single(r => r.InvoiceType == InvoiceType.Sale && r.Number == 2).IsOverdue);   // تسویه
        Assert.False(all.Rows.Single(r => r.InvoiceType == InvoiceType.Sale && r.Number == 5).IsOverdue);   // پیش‌نویس
        Assert.False(all.Rows.Single(r => r.InvoiceType == InvoiceType.Sale && r.Number == 6).IsOverdue);   // تسویه با تخفیف

        // سررسید در ردیف‌ها برمی‌گردد
        Assert.Equal(new DateOnly(2026, 6, 20), all.Rows.Single(r => r.InvoiceType == InvoiceType.Sale && r.Number == 1).DueDate);
        Assert.Null(all.Rows.Single(r => r.InvoiceType == InvoiceType.Sale && r.Number == 4).DueDate);

        // فیلتر «فقط معوق»؛ جمع‌ها هم روی همان
        var onlyOverdue = await NewReportService().GetInvoicesAsync(
            new InvoiceReportQuery { Today = Today, OnlyOverdue = true, PageSize = 100 });

        Assert.Equal(2, onlyOverdue.Count);
        Assert.All(onlyOverdue.Rows, r => Assert.True(r.IsOverdue));
        Assert.Equal(1_700, onlyOverdue.TotalRemaining);
    }

    [SqlServerFact]
    public async Task InvoiceReport_WithoutToday_NeverReportsOverdue()
    {
        var contact = await AddContactAsync("الف");
        await SeedInvoicesAsync(contact);

        // سازگاری با فراخوانی‌های قدیمی (Today = null): هیچ فاکتوری معوق شمرده نمی‌شود
        var report = await NewReportService().GetInvoicesAsync(new InvoiceReportQuery { PageSize = 100, OnlyOverdue = false });

        Assert.Equal(0, report.OverdueCount);
        Assert.All(report.Rows, r => Assert.False(r.IsOverdue));

        var onlyOverdueWithoutToday = await NewReportService().GetInvoicesAsync(
            new InvoiceReportQuery { PageSize = 100, OnlyOverdue = true });

        Assert.Empty(onlyOverdueWithoutToday.Rows);
    }

    // ---------------------------------------------------------------
    // داشبورد
    // ---------------------------------------------------------------

    [SqlServerFact]
    public async Task Dashboard_ShowsOverdueKpis_AndTheDueInvoiceLists()
    {
        var contact = await AddContactAsync("الف");
        await SeedInvoicesAsync(contact);

        var summary = await NewDashboardService().GetSummaryAsync(dueWindowDays: 7, topN: 5);

        Assert.Equal(1, summary.OverdueSaleCount);
        Assert.Equal(1_000, summary.OverdueSaleRemaining);
        Assert.Equal(1, summary.OverduePurchaseCount);
        Assert.Equal(700, summary.OverduePurchaseRemaining);

        // فروش: معوق (06-20) سپس نزدیک (07-05)؛ بدون سررسید، تسویه‌شده و پیش‌نویس نیامده
        Assert.Equal(new[] { 1, 3 }, summary.DueSaleInvoices.Select(i => i.Number).ToArray());
        Assert.Equal(new[] { -11, 4 }, summary.DueSaleInvoices.Select(i => i.DaysLeft).ToArray());
        Assert.True(summary.DueSaleInvoices[0].IsOverdue);
        Assert.False(summary.DueSaleInvoices[1].IsOverdue);
        Assert.Equal(1_000, summary.DueSaleInvoices[0].Remaining);
        Assert.Equal("الف", summary.DueSaleInvoices[0].ContactName);

        // خرید: معوق (06-30) و سررسیدِ امروز
        Assert.Equal(new[] { 1, 2 }, summary.DuePurchaseInvoices.Select(i => i.Number).ToArray());
        Assert.Equal(new[] { -1, 0 }, summary.DuePurchaseInvoices.Select(i => i.DaysLeft).ToArray());
    }

    [SqlServerFact]
    public async Task Dashboard_RespectsTheDueWindow()
    {
        var contact = await AddContactAsync("الف");
        await SeedInvoicesAsync(contact);

        // پنجره‌ی 2 روزه: سررسید 07-05 (4 روز بعد) نمی‌آید
        var narrow = await NewDashboardService().GetSummaryAsync(dueWindowDays: 2, topN: 5);
        Assert.Equal(new[] { 1 }, narrow.DueSaleInvoices.Select(i => i.Number).ToArray());

        // «معوق» به پنجره وابسته نیست
        Assert.Equal(1, narrow.OverdueSaleCount);
    }

    // ---------------------------------------------------------------
    // گزارش بدهکاران
    // ---------------------------------------------------------------

    [SqlServerFact]
    public async Task Debtors_ShowsOverdueRemaining_RelativeToTheReportDate()
    {
        var receivable = await _db.AddAccountAsync(Tenant, "دریافتنی", AccountType.Receivable);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);
        var contact = await AddContactAsync("الف");

        // دفتر: مشتری 1300 بدهکار شده
        await AddDocumentAsync(new DateOnly(2026, 6, 1), receivable, sales, 1_300, contact);

        // فاکتورهای باز: یکی سررسید گذشته (06-20)، یکی آینده (07-05)، یکی بدون سررسید
        await AddInvoiceAsync(InvoiceType.Sale, 1, contact, new DateOnly(2026, 6, 1), 1_000, due: new DateOnly(2026, 6, 20));
        await AddInvoiceAsync(InvoiceType.Sale, 2, contact, new DateOnly(2026, 6, 1), 300, due: new DateOnly(2026, 7, 5));
        await AddInvoiceAsync(InvoiceType.Sale, 3, contact, new DateOnly(2026, 6, 1), 50, due: null);

        var asOfToday = await NewReportService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = Today, Mode = ContactBalanceMode.Debtors });

        var row = Assert.Single(asOfToday.Rows);
        Assert.Equal(1_300, row.Amount);
        Assert.Equal(1_000, row.OverdueAmount);            // فقط فاکتورِ سررسیدگذشته
        Assert.Equal(1_000, asOfToday.TotalOverdue);

        // نسبت به تاریخ گزارش سنجیده می‌شود: تا 06-10 هنوز هیچ‌کدام معوق نیست
        var early = await NewReportService().GetContactBalancesAsync(
            new ContactBalancesQuery { AsOf = new DateOnly(2026, 6, 10), Mode = ContactBalanceMode.Debtors });

        Assert.Equal(0, Assert.Single(early.Rows).OverdueAmount);

        // بعد از 07-05 فاکتور دوم هم معوق می‌شود
        var late = await NewReportService().GetContactBalancesAsync(
            new ContactBalancesQuery { AsOf = new DateOnly(2026, 7, 10), Mode = ContactBalanceMode.Debtors });

        Assert.Equal(1_300, Assert.Single(late.Rows).OverdueAmount);
    }

    // ---------------------------------------------------------------
    // Arrange
    // ---------------------------------------------------------------

    /// <summary>
    /// فروش: 1) 1000 سررسید 06-20 ← معوق    2) 500 تسویه‌شده   3) 300 سررسید 07-05 ← آینده
    ///       4) 200 بدون سررسید             5) 400 پیش‌نویس    6) 150 با تخفیف تسویه ← باقی‌مانده 0
    /// خرید: 1) 700 سررسید 06-30 ← معوق    2) 90 سررسید امروز (07-01) ← معوق نیست
    /// </summary>
    private async Task SeedInvoicesAsync(int contact)
    {
        await AddInvoiceAsync(InvoiceType.Sale, 1, contact, new DateOnly(2026, 6, 1), 1_000, due: new DateOnly(2026, 6, 20));
        await AddInvoiceAsync(InvoiceType.Sale, 2, contact, new DateOnly(2026, 6, 1), 500, paid: 500, due: new DateOnly(2026, 6, 1));
        await AddInvoiceAsync(InvoiceType.Sale, 3, contact, new DateOnly(2026, 6, 1), 300, due: new DateOnly(2026, 7, 5));
        await AddInvoiceAsync(InvoiceType.Sale, 4, contact, new DateOnly(2026, 6, 1), 200, due: null);
        await AddInvoiceAsync(InvoiceType.Sale, 5, contact, new DateOnly(2026, 6, 1), 400, due: new DateOnly(2026, 6, 10), status: InvoiceStatus.Draft);
        await AddInvoiceAsync(InvoiceType.Sale, 6, contact, new DateOnly(2026, 6, 1), 150, paid: 50, discount: 100, due: new DateOnly(2026, 6, 1));

        await AddInvoiceAsync(InvoiceType.Purchase, 1, contact, new DateOnly(2026, 6, 1), 700, due: new DateOnly(2026, 6, 30));
        await AddInvoiceAsync(InvoiceType.Purchase, 2, contact, new DateOnly(2026, 6, 1), 90, due: new DateOnly(2026, 7, 1));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private ReportService NewReportService() => new(_db.NewContext(), _db.Tenant);

    private DashboardService NewDashboardService()
    {
        var context = _db.NewContext();
        return new DashboardService(context, _db.Tenant, new FiscalYearService(context, _db.User, _time), _time);
    }

    private async Task<int> AddContactAsync(string name)
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = name, ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private async Task AddInvoiceAsync(
        InvoiceType type, int number, int contactId, DateOnly date, decimal total,
        decimal paid = 0, decimal discount = 0, DateOnly? due = null, InvoiceStatus status = InvoiceStatus.Posted)
    {
        await using var context = _db.NewContext();
        context.Invoices.Add(new Invoice
        {
            Number = number,
            InvoiceType = type,
            Status = status,
            ContactId = contactId,
            Date = date,
            DueDate = due,
            TotalAmount = total,
            PaidAmount = paid,
            SettlementDiscount = discount
        });
        await context.SaveChangesAsync();
    }

    private async Task AddDocumentAsync(DateOnly date, int debitAccount, int creditAccount, decimal amount, int debitContact)
    {
        await using var context = _db.NewContext();
        var service = new TransactionService(context);

        var created = await service.CreateAsync(new Transaction
        {
            TenantId = Tenant,
            Date = date,
            Lines =
            {
                new TransactionLine { AccountId = debitAccount, ContactId = debitContact, Debit = amount },
                new TransactionLine { AccountId = creditAccount, Credit = amount }
            }
        });

        await service.PostAsync(created.Id, Tenant);
    }
}