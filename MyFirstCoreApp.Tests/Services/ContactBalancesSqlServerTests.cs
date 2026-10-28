using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Application.Reports;
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

/// <summary>
/// پروسیجر بدهکاران/بستانکاران روی SQL Server واقعی (از طریق ReportService).
/// بدون متغیر محیطی MYFIRSTCOREAPP_TEST_SQLSERVER، Skip می‌شود.
///
/// سناریو (امروزِ گزارش = 2026-04-11):
///   الف: بدهکار 1000 (01-10) و بستانکار 300 (02-10) ← مانده 700
///        فاکتور فروش باز 1000/پرداخت 300 به تاریخ 01-10 (سن 91 روز) ← «بیش از 90»
///        فاکتور خرید باز (نباید در بدهکاران بیاید) ، چک دریافتنی در انتظار 200، چک پرداختنی (نباید بیاید)
///   ب:   بدهکار 500 (03-01) + یک سند بعد از تاریخ گزارش (06-01) ← مانده 500
///        فاکتور فروش باز 500 (سن 41 روز)، چک دریافتنیِ پاس‌شده (نباید بیاید)
///   ج:   بستانکار 400 (02-01) ← فقط در بستانکاران
///   د:   بدهکار 200 و بستانکار 200 ← مانده صفر، در هیچ‌کدام نیست
/// </summary>
public class ContactBalancesSqlServerTests : IDisposable
{
    private const int Tenant = 1;
    private const int OtherTenant = 2;

    private static readonly DateOnly AsOf = new(2026, 4, 11);

    private readonly SqlServerTestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 4, 11, 8, 30, 0, TimeSpan.Zero));

    public ContactBalancesSqlServerTests()
    {
        _db.Tenant.TenantId = Tenant;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    [SqlServerFact]
    public async Task Debtors_ComputesBalanceAgingAndPendingCheques_ForPostedLedgerOnly()
    {
        var f = await ArrangeAsync();

        var report = await NewService().GetContactBalancesAsync(
            new ContactBalancesQuery { Mode = ContactBalanceMode.Debtors, AsOf = AsOf });

        // الف و ب؛ ج (بستانکار) و د (مانده‌ی صفر) نیستند. بیشترین مانده اول.
        Assert.Equal(new[] { f.A, f.B }, report.Rows.Select(r => r.ContactId).ToArray());
        Assert.Equal(2, report.Count);
        Assert.Equal(1200, report.TotalAmount);

        var a = report.Rows[0];
        Assert.Equal("الف", a.ContactName);
        Assert.Equal(1000, a.TotalDebit);
        Assert.Equal(300, a.TotalCredit);
        Assert.Equal(700, a.Amount);
        Assert.Equal(new DateOnly(2026, 2, 10), a.LastTransactionDate);

        // فقط فاکتور «فروش»ِ باز؛ فاکتور خرید و پیش‌نویس‌ها شمرده نمی‌شوند
        Assert.Equal(1, a.OpenInvoiceCount);
        Assert.Equal(700, a.OpenInvoiceRemaining);
        Assert.Equal(700, a.Age90Plus);                    // 91 روز
        Assert.Equal(0, a.Age0To30 + a.Age31To60 + a.Age61To90);
        Assert.Equal(0, a.OtherAmount);                    // مانده = فاکتورهای باز

        // فقط چکِ «دریافتنیِ در انتظار» (نه پرداختنی)
        Assert.Equal(200, a.PendingChequeAmount);
        Assert.Equal(900, a.AmountIfChequesBounce);

        var b = report.Rows[1];
        Assert.Equal(500, b.Amount);                       // سند بعد از تاریخ گزارش نیامده
        Assert.Equal(500, b.Age31To60);                    // 41 روز
        Assert.Equal(0, b.PendingChequeAmount);            // چک پاس‌شده شمرده نمی‌شود

        // جمع‌ها روی همه‌ی ردیف‌ها
        Assert.Equal(1200, report.TotalOpenInvoiceRemaining);
        Assert.Equal(700, report.TotalAge90Plus);
        Assert.Equal(500, report.TotalAge31To60);
        Assert.Equal(200, report.TotalPendingCheque);
        Assert.Equal(1400, report.TotalIfChequesBounce);
        Assert.Equal(0, report.TotalOther);
    }

    [SqlServerFact]
    public async Task Creditors_ShowsThePositiveAmountWeOwe()
    {
        var f = await ArrangeAsync();

        var report = await NewService().GetContactBalancesAsync(
            new ContactBalancesQuery { Mode = ContactBalanceMode.Creditors, AsOf = AsOf });

        var row = Assert.Single(report.Rows);
        Assert.Equal(f.C, row.ContactId);
        Assert.Equal(400, row.Amount);                     // مثبت (بستانکار − بدهکار)
        Assert.Equal(0, row.TotalDebit);
        Assert.Equal(400, row.TotalCredit);
        Assert.Equal(400, report.TotalAmount);

        // «سایر»: بدهی ما که فاکتور خریدِ بازی ندارد (سند دستی)
        Assert.Equal(400, row.OtherAmount);
    }

    [SqlServerFact]
    public async Task AsOfDate_ExcludesLaterDocuments_AndContactsThatWereNotYetDebtors()
    {
        var f = await ArrangeAsync();

        // 2026-02-28: «ب» هنوز بدهکار نشده (سندش 03-01 است)
        var early = await NewService().GetContactBalancesAsync(
            new ContactBalancesQuery { Mode = ContactBalanceMode.Debtors, AsOf = new DateOnly(2026, 2, 28) });

        Assert.Equal(new[] { f.A }, early.Rows.Select(r => r.ContactId).ToArray());
        Assert.Equal(700, early.Rows[0].Amount);

        // بعد از سند 06-01 «ب»: 500 + 100
        var late = await NewService().GetContactBalancesAsync(
            new ContactBalancesQuery { Mode = ContactBalanceMode.Debtors, AsOf = new DateOnly(2026, 7, 1) });

        Assert.Equal(600, late.Rows.Single(r => r.ContactId == f.B).Amount);
    }

    [SqlServerFact]
    public async Task Filters_MinAmount_Search_Sort_AndPaging()
    {
        var f = await ArrangeAsync();

        var big = await NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf, MinAmount = 600 });
        Assert.Equal(new[] { f.A }, big.Rows.Select(r => r.ContactId).ToArray());

        // جستجو با نام، موبایل و کد
        var byName = await NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf, Search = "ب" });
        Assert.Equal(new[] { f.B }, byName.Rows.Select(r => r.ContactId).ToArray());

        var byMobile = await NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf, Search = "0912" });
        Assert.Equal(new[] { f.A }, byMobile.Rows.Select(r => r.ContactId).ToArray());

        // کاراکترهای ویژه‌ی LIKE خنثی می‌شوند (٪ نباید همه را بیاورد)
        var wildcard = await NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf, Search = "%" });
        Assert.Empty(wildcard.Rows);

        // مرتب‌سازی بر اساس نام و قدیمی‌ترین فعالیت (ب: 03-01 جدیدتر از الف: 02-10 است)
        var byNameSorted = await NewService().GetContactBalancesAsync(
            new ContactBalancesQuery { AsOf = AsOf, SortBy = ContactBalanceSort.Name });
        Assert.Equal(new[] { f.A, f.B }, byNameSorted.Rows.Select(r => r.ContactId).ToArray());

        var oldest = await NewService().GetContactBalancesAsync(
            new ContactBalancesQuery { AsOf = AsOf, SortBy = ContactBalanceSort.OldestActivity });
        Assert.Equal(new[] { f.A, f.B }, oldest.Rows.Select(r => r.ContactId).ToArray());

        // صفحه‌بندی: جمع کل روی همه‌ی ردیف‌هاست، نه فقط صفحه
        var page2 = await NewService().GetContactBalancesAsync(
            new ContactBalancesQuery { AsOf = AsOf, PageNumber = 2, PageSize = 1 });
        Assert.Equal(new[] { f.B }, page2.Rows.Select(r => r.ContactId).ToArray());
        Assert.Equal(2, page2.Count);
        Assert.Equal(2, page2.TotalPages);
        Assert.Equal(1200, page2.TotalAmount);

        // PageSize = 0 ← همه
        var all = await NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf, PageSize = 0 });
        Assert.Equal(2, all.Rows.Count);
        Assert.Equal(1, all.TotalPages);
    }

    [SqlServerFact]
    public async Task OtherTenantsContacts_AreNeverIncluded()
    {
        await ArrangeAsync();

        // شرکت 2: یک بدهکار بزرگ
        _db.Tenant.TenantId = OtherTenant;
        var receivable = await _db.AddAccountAsync(OtherTenant, "دریافتنی", AccountType.Receivable);
        var sales = await _db.AddAccountAsync(OtherTenant, "فروش", AccountType.Income);
        var stranger = await AddContactAsync("غریبه", tenant: OtherTenant);
        await AddDocumentAsync(new DateOnly(2026, 3, 1), receivable, sales, 9_999_999, debitContact: stranger, tenant: OtherTenant);

        _db.Tenant.TenantId = Tenant;

        var report = await NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf });

        Assert.Equal(1200, report.TotalAmount);
        Assert.DoesNotContain(report.Rows, r => r.ContactName == "غریبه");
    }

    [SqlServerFact]
    public async Task InvalidQueries_AreRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService().GetContactBalancesAsync(new ContactBalancesQuery()));                       // بدون تاریخ

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf, MinAmount = -1 }));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf, Mode = (ContactBalanceMode)9 }));

        _db.Tenant.TenantId = null;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService().GetContactBalancesAsync(new ContactBalancesQuery { AsOf = AsOf }));
    }

    // ---------------------------------------------------------------
    // Arrange
    // ---------------------------------------------------------------

    private sealed record Fixture(int A, int B, int C, int D);

    private async Task<Fixture> ArrangeAsync()
    {
        // اسناد دو سال شمسی را می‌پوشانند (1404 و 1405)؛ سال خودکار فقط برای «اولین سند» ساخته می‌شود
        await CreateYearAsync(new DateOnly(2025, 3, 21), new DateOnly(2026, 3, 20));
        await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));

        var receivable = await _db.AddAccountAsync(Tenant, "دریافتنی", AccountType.Receivable);
        var payable = await _db.AddAccountAsync(Tenant, "پرداختنی", AccountType.Payable);
        var sales = await _db.AddAccountAsync(Tenant, "فروش", AccountType.Income);
        var purchases = await _db.AddAccountAsync(Tenant, "خرید", AccountType.Expense);
        var bank = await _db.AddAccountAsync(Tenant, "بانک", AccountType.Bank);

        var a = await AddContactAsync("الف", mobile: "09121111111", code: "C-001");
        var b = await AddContactAsync("ب", mobile: "09352222222", code: "C-002");
        var c = await AddContactAsync("ج");
        var d = await AddContactAsync("د");

        // ---------- دفتر ----------
        await AddDocumentAsync(new DateOnly(2026, 1, 10), receivable, sales, 1000, debitContact: a);
        await AddDocumentAsync(new DateOnly(2026, 2, 10), bank, receivable, 300, creditContact: a);
        await AddDocumentAsync(new DateOnly(2026, 3, 1), receivable, sales, 500, debitContact: b);
        await AddDocumentAsync(new DateOnly(2026, 6, 1), receivable, sales, 100, debitContact: b);          // بعد از تاریخ گزارش
        await AddDocumentAsync(new DateOnly(2026, 2, 1), purchases, payable, 400, creditContact: c);
        await AddDocumentAsync(new DateOnly(2026, 1, 20), receivable, sales, 200, debitContact: d);
        await AddDocumentAsync(new DateOnly(2026, 1, 25), bank, receivable, 200, creditContact: d);

        // پیش‌نویس هیچ‌جا نباید بیاید
        await AddDocumentAsync(new DateOnly(2026, 4, 1), receivable, sales, 77_777, debitContact: c, post: false);

        // ---------- فاکتورها ----------
        await AddInvoiceAsync(1, InvoiceType.Sale, InvoiceStatus.Posted, a, new DateOnly(2026, 1, 10), total: 1000, paid: 300);
        await AddInvoiceAsync(2, InvoiceType.Purchase, InvoiceStatus.Posted, a, new DateOnly(2026, 1, 15), total: 999);   // هم‌نوع نیست
        await AddInvoiceAsync(3, InvoiceType.Sale, InvoiceStatus.Draft, a, new DateOnly(2026, 4, 1), total: 5_555);       // پیش‌نویس
        await AddInvoiceAsync(4, InvoiceType.Sale, InvoiceStatus.Posted, b, new DateOnly(2026, 3, 1), total: 500);

        // ---------- چک‌ها ----------
        await AddChequeAsync("R1", ChequeDirection.Received, ChequeStatus.InProgress, a, 200);
        await AddChequeAsync("I1", ChequeDirection.Issued, ChequeStatus.InProgress, a, 77);       // جهت نامناسب
        await AddChequeAsync("R2", ChequeDirection.Received, ChequeStatus.Cleared, b, 50);        // وضعیت نامناسب

        return new Fixture(a, b, c, d);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private ReportService NewService() => new(_db.NewContext(), _db.Tenant);

    private async Task CreateYearAsync(DateOnly start, DateOnly end)
    {
        await using var context = _db.NewContext();
        await new FiscalYearService(context, _db.User, _time)
            .CreateAsync(new FiscalYearRequest { StartDate = start, EndDate = end }, Tenant);
    }

    private async Task<int> AddContactAsync(string name, string? mobile = null, string? code = null, int tenant = Tenant)
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenant;

        try
        {
            await using var context = _db.NewContext();
            var contact = new Contact
            {
                Name = name,
                Mobile = mobile,
                Code = code,
                ContactType = ContactType.Both,
                IsActive = true
            };
            context.Contacts.Add(contact);
            await context.SaveChangesAsync();
            return contact.Id;
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }

    private async Task AddDocumentAsync(
        DateOnly date, int debitAccount, int creditAccount, decimal amount,
        int? debitContact = null, int? creditContact = null, bool post = true, int tenant = Tenant)
    {
        await using var context = _db.NewContext();
        var service = new TransactionService(context);

        var created = await service.CreateAsync(new Transaction
        {
            TenantId = tenant,
            Date = date,
            Lines =
            {
                new TransactionLine { AccountId = debitAccount, ContactId = debitContact, Debit = amount },
                new TransactionLine { AccountId = creditAccount, ContactId = creditContact, Credit = amount }
            }
        });

        if (post)
            await service.PostAsync(created.Id, tenant);
    }

    private async Task AddInvoiceAsync(
        int number, InvoiceType type, InvoiceStatus status, int contactId, DateOnly date, decimal total, decimal paid = 0)
    {
        await using var context = _db.NewContext();
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
    }

    private async Task AddChequeAsync(
        string number, ChequeDirection direction, ChequeStatus status, int contactId, decimal amount)
    {
        await using var context = _db.NewContext();
        context.Cheques.Add(new Cheque
        {
            ChequeNumber = number,
            Direction = direction,
            Status = status,
            ContactId = contactId,
            IssueDate = new DateOnly(2026, 3, 20),
            DueDate = new DateOnly(2026, 6, 20),
            Amount = amount
        });
        await context.SaveChangesAsync();
    }
}
