using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Application.Reports;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// تست Stored Procedureها روی SQL Server واقعی (از طریق ReportService و Dapper).
/// بدون متغیر محیطی MYFIRSTCOREAPP_TEST_SQLSERVER این تست‌ها Skip می‌شوند.
/// </summary>
public class ReportServiceSqlServerTests : IDisposable
{
    private const int Tenant = 1;
    private const int OtherTenant = 2;

    private readonly SqlServerTestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

    public ReportServiceSqlServerTests()
    {
        _db.Tenant.TenantId = Tenant;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // معین
    // ---------------------------------------------------------------

    [SqlServerFact]
    public async Task Ledger_ComputesOpeningRunningBalanceAndTotals_ForPostedDocumentsOnly()
    {
        var bank = await _db.AddAccountAsync(Tenant, "بانک", AccountType.Bank);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);

        await AddDocumentAsync(new DateOnly(2026, 3, 25), bank, sales, 100);          // قبل از بازه ← مانده‌ی اول دوره
        await AddDocumentAsync(new DateOnly(2026, 4, 10), bank, sales, 200);
        await AddDocumentAsync(new DateOnly(2026, 4, 12), bank, sales, 50, post: false);   // پیش‌نویس ← نباید بیاید
        var deleted = await AddDocumentAsync(new DateOnly(2026, 4, 15), bank, sales, 25, post: false);
        await DeleteDraftAsync(deleted);                                                // حذف‌شده ← نباید بیاید
        await AddDocumentAsync(new DateOnly(2026, 5, 1), bank, sales, 400);            // بعد از بازه

        var april = await NewService().GetLedgerAsync(new LedgerReportQuery
        {
            AccountId = bank,
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 4, 30)
        });

        Assert.Equal("بانک", april.AccountName);
        Assert.Equal(100, april.OpeningBalance);
        Assert.Equal(200, april.TotalDebit);
        Assert.Equal(0, april.TotalCredit);
        Assert.Equal(300, april.ClosingBalance);

        var line = Assert.Single(april.Lines);
        Assert.Equal(new DateOnly(2026, 4, 10), line.Date);
        Assert.Equal(300, line.RunningBalance);

        // کل بازه: مانده‌ی تجمعی ردیف‌به‌ردیف
        var all = await NewService().GetLedgerAsync(new LedgerReportQuery
        {
            AccountId = bank,
            From = new DateOnly(2026, 3, 1),
            To = new DateOnly(2026, 12, 31)
        });

        Assert.Equal(0, all.OpeningBalance);
        Assert.Equal(new decimal[] { 100, 300, 700 }, all.Lines.Select(l => l.RunningBalance).ToArray());
        Assert.Equal(700, all.ClosingBalance);
    }

    [SqlServerFact]
    public async Task Ledger_ForIncomeAccount_StartsFromTheFiscalYearStart_ButBankIsCumulative()
    {
        await CreateYearAsync(new DateOnly(2025, 3, 21), new DateOnly(2026, 3, 20));   // 1404
        await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));   // 1405

        var bank = await _db.AddAccountAsync(Tenant, "بانک", AccountType.Bank);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);

        await AddDocumentAsync(new DateOnly(2025, 10, 1), bank, sales, 1000);          // سال قبل
        await AddDocumentAsync(new DateOnly(2026, 6, 1), bank, sales, 300);            // سال جاری

        var query = new DateOnly(2026, 7, 1);
        var end = new DateOnly(2026, 12, 29);

        var incomeReport = await NewService().GetLedgerAsync(new LedgerReportQuery { AccountId = sales, From = query, To = end });
        var bankReport = await NewService().GetLedgerAsync(new LedgerReportQuery { AccountId = bank, From = query, To = end });

        // درآمد: فقط 300 سال جاری (بستانکار = منفی)؛ 1000 سال قبل شمرده نمی‌شود
        Assert.Equal(-300, incomeReport.OpeningBalance);

        // بانک: دائمی ← تجمعی (1000 + 300)
        Assert.Equal(1300, bankReport.OpeningBalance);
    }

    [SqlServerFact]
    public async Task Ledger_NeverReturnsAnotherTenantsData_OrAccount()
    {
        var bank = await _db.AddAccountAsync(Tenant, "بانک", AccountType.Bank);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);
        await AddDocumentAsync(new DateOnly(2026, 4, 10), bank, sales, 200);

        // شرکت 2 همان تاریخ‌ها را دارد
        _db.Tenant.TenantId = OtherTenant;
        var otherBank = await _db.AddAccountAsync(OtherTenant, "بانک دیگر", AccountType.Bank);
        var otherSales = await _db.AddAccountAsync(OtherTenant, "فروش دیگر", AccountType.Income);
        await AddDocumentAsync(new DateOnly(2026, 4, 10), otherBank, otherSales, 9_999, tenant: OtherTenant);

        _db.Tenant.TenantId = Tenant;

        var report = await NewService().GetLedgerAsync(new LedgerReportQuery
        {
            AccountId = bank,
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 4, 30)
        });

        Assert.Equal(200, report.TotalDebit);

        // حسابِ شرکت دیگر برای این شرکت وجود ندارد
        await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().GetLedgerAsync(new LedgerReportQuery
        {
            AccountId = otherBank,
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 4, 30)
        }));
    }

    // ---------------------------------------------------------------
    // تفصیلی
    // ---------------------------------------------------------------

    [SqlServerFact]
    public async Task Detailed_ContactFilter_AppliesToOpeningBalanceToo()
    {
        var receivable = await _db.AddAccountAsync(Tenant, "دریافتنی", AccountType.Receivable);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);
        var contactA = await AddContactAsync("الف");
        var contactB = await AddContactAsync("ب");

        await AddDocumentAsync(new DateOnly(2026, 4, 1), receivable, sales, 100, debitContactId: contactA);
        await AddDocumentAsync(new DateOnly(2026, 4, 5), receivable, sales, 70, debitContactId: contactB);
        await AddDocumentAsync(new DateOnly(2026, 5, 1), receivable, sales, 30, debitContactId: contactA);

        var forA = await NewService().GetDetailedAsync(new DetailedReportQuery
        {
            AccountId = receivable,
            ContactId = contactA,
            From = new DateOnly(2026, 4, 20),
            To = new DateOnly(2026, 5, 31)
        });

        // مانده‌ی اول دوره فقط از «الف» (قبلاً 170 می‌شد و با خطوط نمایش‌داده‌شده نمی‌خواند)
        Assert.Equal(100, forA.OpeningBalance);
        var line = Assert.Single(forA.Lines);
        Assert.Equal("الف", line.ContactName);
        Assert.Equal(130, line.RunningBalance);
        Assert.Equal(130, forA.ClosingBalance);

        var all = await NewService().GetDetailedAsync(new DetailedReportQuery
        {
            AccountId = receivable,
            From = new DateOnly(2026, 4, 20),
            To = new DateOnly(2026, 5, 31)
        });

        Assert.Equal(170, all.OpeningBalance);
        Assert.Equal(200, all.ClosingBalance);
    }

    [SqlServerFact]
    public async Task Detailed_OnlyPostedFalse_IncludesDrafts()
    {
        var bank = await _db.AddAccountAsync(Tenant, "بانک", AccountType.Bank);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);

        await AddDocumentAsync(new DateOnly(2026, 4, 10), bank, sales, 200);
        await AddDocumentAsync(new DateOnly(2026, 4, 12), bank, sales, 50, post: false);

        var range = new DateOnly(2026, 4, 1);
        var end = new DateOnly(2026, 4, 30);

        var posted = await NewService().GetDetailedAsync(new DetailedReportQuery { AccountId = bank, From = range, To = end, OnlyPosted = true });
        var withDrafts = await NewService().GetDetailedAsync(new DetailedReportQuery { AccountId = bank, From = range, To = end, OnlyPosted = false });

        Assert.Single(posted.Lines);
        Assert.Equal(2, withDrafts.Lines.Count);
        Assert.Equal(250, withDrafts.ClosingBalance);
    }

    // ---------------------------------------------------------------
    // صورت‌حساب طرف حساب
    // ---------------------------------------------------------------

    [SqlServerFact]
    public async Task Contact_UsesCumulativeOpening_AndShowsAccountNames()
    {
        var receivable = await _db.AddAccountAsync(Tenant, "دریافتنی", AccountType.Receivable);
        var instrument = await _db.AddAccountAsync(Tenant, "چک‌های دریافتنی", AccountType.Contra);
        var contact = await AddContactAsync("مشتری");

        // فاکتور: دریافتنی بدهکار (مشتری 1000 بدهکار شد)
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);
        await AddDocumentAsync(new DateOnly(2026, 3, 25), receivable, sales, 1000, debitContactId: contact);

        // چک دریافتی: حساب چک بدهکار، طرف حساب بستانکار (از 1000 کم می‌شود)
        await AddDocumentAsync(new DateOnly(2026, 4, 10), instrument, receivable, 400, creditContactId: contact);

        var report = await NewService().GetContactAsync(new ContactReportQuery
        {
            ContactId = contact,
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 4, 30)
        });

        Assert.Equal("مشتری", report.ContactName);
        Assert.Equal(1000, report.OpeningBalance);
        var line = Assert.Single(report.Lines);
        Assert.Equal("دریافتنی", line.AccountName);
        Assert.Equal(400, line.Credit);
        Assert.Equal(600, line.RunningBalance);
        Assert.Equal(600, report.ClosingBalance);
    }

    // ---------------------------------------------------------------
    // فاکتورها
    // ---------------------------------------------------------------

    [SqlServerFact]
    public async Task Invoices_FiltersTotalsPagingAndRemainingWithSettlementDiscount()
    {
        var contactA = await AddContactAsync("الف");
        var contactB = await AddContactAsync("ب");

        await AddInvoiceAsync(1, InvoiceType.Sale, InvoiceStatus.Posted, contactA, new DateOnly(2026, 4, 1), total: 1000, paid: 400);
        await AddInvoiceAsync(2, InvoiceType.Sale, InvoiceStatus.Posted, contactA, new DateOnly(2026, 4, 2), total: 500, paid: 100, discount: 400);   // باقی‌مانده 0
        await AddInvoiceAsync(3, InvoiceType.Sale, InvoiceStatus.Draft, contactB, new DateOnly(2026, 4, 3), total: 300);
        await AddInvoiceAsync(4, InvoiceType.Purchase, InvoiceStatus.Posted, contactB, new DateOnly(2026, 4, 4), total: 200, paid: 50);
        await AddInvoiceAsync(5, InvoiceType.Sale, InvoiceStatus.Posted, contactB, new DateOnly(2026, 6, 1), total: 800);

        // فاکتور شرکت دیگر و فاکتور حذف‌شده نباید بیایند
        await AddInvoiceAsync(9, InvoiceType.Sale, InvoiceStatus.Posted, contactA, new DateOnly(2026, 4, 5), total: 7_777, tenant: OtherTenant);
        var toDelete = await AddInvoiceAsync(8, InvoiceType.Sale, InvoiceStatus.Draft, contactA, new DateOnly(2026, 4, 6), total: 6_666);
        await DeleteInvoiceAsync(toDelete);

        // بدون فیلتر: جمع‌ها روی همه‌ی ردیف‌های مجاز
        var all = await NewService().GetInvoicesAsync(new InvoiceReportQuery { PageSize = 100 });

        Assert.Equal(5, all.Count);
        Assert.Equal(1000 + 500 + 300 + 200 + 800, all.TotalAmount);
        Assert.Equal(400 + 100 + 0 + 50 + 0, all.TotalPaid);
        Assert.Equal(400, all.TotalSettlementDiscount);
        Assert.Equal(600 + 0 + 300 + 150 + 800, all.TotalRemaining);

        // جدیدترین اول
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, all.Rows.Select(r => r.Number).ToArray());

        // باقی‌مانده با تخفیف تسویه
        Assert.Equal(0, all.Rows.Single(r => r.Number == 2).RemainingAmount);

        // فقط فروش + فقط دارای باقی‌مانده + بازه‌ی تاریخ
        var open = await NewService().GetInvoicesAsync(new InvoiceReportQuery
        {
            InvoiceType = InvoiceType.Sale,
            OnlyOpen = true,
            From = new DateOnly(2026, 4, 1),
            To = new DateOnly(2026, 4, 30),
            PageSize = 100
        });

        Assert.Equal(new[] { 3, 1 }, open.Rows.Select(r => r.Number).ToArray());
        Assert.Equal(900, open.TotalRemaining);

        // وضعیت و طرف حساب
        var postedOfB = await NewService().GetInvoicesAsync(new InvoiceReportQuery
        {
            Status = InvoiceStatus.Posted,
            ContactId = contactB,
            PageSize = 100
        });

        Assert.Equal(new[] { 5, 4 }, postedOfB.Rows.Select(r => r.Number).ToArray());

        // صفحه‌بندی: جمع کل روی همه‌ی ردیف‌هاست، نه فقط صفحه
        var page2 = await NewService().GetInvoicesAsync(new InvoiceReportQuery { PageNumber = 2, PageSize = 2 });

        Assert.Equal(new[] { 3, 2 }, page2.Rows.Select(r => r.Number).ToArray());
        Assert.Equal(5, page2.Count);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(2800, page2.TotalAmount);
    }

    [SqlServerFact]
    public async Task Reports_RequireAnActiveTenant()
    {
        _db.Tenant.TenantId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewService().GetInvoicesAsync(new InvoiceReportQuery()));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private ReportService NewService() => new(_db.NewContext(), _db.Tenant);

    private async Task<int> AddContactAsync(string name)
    {
        await using var context = _db.NewContext();
        var contact = new Contact { Name = name, ContactType = ContactType.Both, IsActive = true };
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private async Task CreateYearAsync(DateOnly start, DateOnly end)
    {
        await using var context = _db.NewContext();
        await new FiscalYearService(context, _db.User, _time)
            .CreateAsync(new FiscalYearRequest { StartDate = start, EndDate = end }, Tenant);
    }

    private async Task<int> AddDocumentAsync(
        DateOnly date, int debitAccount, int creditAccount, decimal amount,
        int? debitContactId = null, int? creditContactId = null, bool post = true, int tenant = Tenant)
    {
        await using var context = _db.NewContext();
        var service = new TransactionService(context);

        var created = await service.CreateAsync(new Transaction
        {
            TenantId = tenant,
            Date = date,
            Lines =
            {
                new TransactionLine { AccountId = debitAccount, ContactId = debitContactId, Debit = amount },
                new TransactionLine { AccountId = creditAccount, ContactId = creditContactId, Credit = amount }
            }
        });

        if (post)
            await service.PostAsync(created.Id, tenant);

        return created.Id;
    }

    private async Task DeleteDraftAsync(int transactionId)
    {
        await using var context = _db.NewContext();
        await new TransactionService(context).DeleteManualAsync(transactionId, Tenant);
    }

    private async Task<int> AddInvoiceAsync(
        int number, InvoiceType type, InvoiceStatus status, int contactId, DateOnly date,
        decimal total, decimal paid = 0, decimal discount = 0, int tenant = Tenant)
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenant;

        try
        {
            await using var context = _db.NewContext();
            var invoice = new Invoice
            {
                Number = number,
                InvoiceType = type,
                Status = status,
                ContactId = contactId,
                Date = date,
                TotalAmount = total,
                PaidAmount = paid,
                SettlementDiscount = discount
            };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            return invoice.Id;
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }

    private async Task DeleteInvoiceAsync(int invoiceId)
    {
        await using var context = _db.NewContext();
        var invoice = await context.Invoices.SingleAsync(i => i.Id == invoiceId);

        context.Invoices.Remove(invoice);
        await context.SaveChangesAsync();
    }
}
