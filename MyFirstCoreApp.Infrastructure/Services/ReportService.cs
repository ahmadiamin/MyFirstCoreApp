using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Reports;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// گزارش‌ها از طریق Stored Procedure و Dapper. این سرویس فقط:
///  - شرکت فعال را می‌گیرد (و بدون آن کار نمی‌کند)،
///  - اطلاعات سربرگ (نام حساب/طرف حساب) را با EF می‌خواند،
///  - سیاست «مانده‌ی اول دوره» را تعیین می‌کند،
///  - پروسیجر را صدا می‌زند و نتیجه را به مدل‌های Application تبدیل می‌کند.
/// تاریخ‌ها با DbType.Date می‌روند و به‌صورت DateTime برمی‌گردند و در مرز سرویس به DateOnly تبدیل می‌شوند.
/// </summary>
public class ReportService : IReportService
{
    private const int MaxPageSize = 1000;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public ReportService(ApplicationDbContext context, ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // ============================================
    // معین
    // ============================================

    public async Task<LedgerReport> GetLedgerAsync(LedgerReportQuery query, CancellationToken cancellationToken = default)
    {
        ValidateRange(query.From, query.To);
        var tenantId = RequireTenant();

        var account = await GetAccountAsync(query.AccountId, tenantId, cancellationToken);
        var openingFrom = await ReportOpeningPolicy.ResolveAsync(
            _context, tenantId, account.AccountType, query.From, cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId, DbType.Int32);
        parameters.Add("AccountId", query.AccountId, DbType.Int32);
        AddDate(parameters, "FromDate", query.From);
        AddDate(parameters, "ToDate", query.To);
        AddDate(parameters, "OpeningFromDate", openingFrom);

        var (summary, rows) = await QueryAsync<SummaryRow, LedgerLineRow>(
            "dbo.usp_Report_Ledger", parameters, cancellationToken);

        return new LedgerReport
        {
            AccountName = account.Name,
            AccountCode = account.Code,
            AccountType = account.AccountType,
            OpeningBalance = summary.OpeningBalance,
            TotalDebit = summary.TotalDebit,
            TotalCredit = summary.TotalCredit,
            ClosingBalance = summary.OpeningBalance + summary.TotalDebit - summary.TotalCredit,
            Lines = rows.Select(r => new LedgerLine
            {
                TransactionId = r.TransactionId,
                TransactionNumber = r.TransactionNumber,
                Date = DateOnly.FromDateTime(r.Date),
                Description = r.Description,
                Reference = r.Reference,
                Debit = r.Debit,
                Credit = r.Credit,
                RunningBalance = r.RunningBalance
            }).ToList()
        };
    }

    // ============================================
    // تفصیلی
    // ============================================

    public async Task<DetailedReport> GetDetailedAsync(DetailedReportQuery query, CancellationToken cancellationToken = default)
    {
        ValidateRange(query.From, query.To);
        var tenantId = RequireTenant();

        var account = await GetAccountAsync(query.AccountId, tenantId, cancellationToken);

        if (query.ContactId is > 0)
        {
            var contactId = query.ContactId.Value;
            var contactExists = await _context.Contacts
                .AnyAsync(c => c.Id == contactId && c.TenantId == tenantId, cancellationToken);

            if (!contactExists)
                throw new InvalidOperationException("طرف حساب پیدا نشد.");
        }

        var openingFrom = await ReportOpeningPolicy.ResolveAsync(
            _context, tenantId, account.AccountType, query.From, cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId, DbType.Int32);
        parameters.Add("AccountId", query.AccountId, DbType.Int32);
        parameters.Add("ContactId", query.ContactId is > 0 ? (int?)query.ContactId : null, DbType.Int32);
        AddDate(parameters, "FromDate", query.From);
        AddDate(parameters, "ToDate", query.To);
        AddDate(parameters, "OpeningFromDate", openingFrom);
        parameters.Add("OnlyPosted", query.OnlyPosted, DbType.Boolean);

        var (summary, rows) = await QueryAsync<SummaryRow, DetailedLineRow>(
            "dbo.usp_Report_Detailed", parameters, cancellationToken);

        return new DetailedReport
        {
            AccountName = account.Name,
            AccountCode = account.Code,
            AccountType = account.AccountType,
            OpeningBalance = summary.OpeningBalance,
            TotalDebit = summary.TotalDebit,
            TotalCredit = summary.TotalCredit,
            ClosingBalance = summary.OpeningBalance + summary.TotalDebit - summary.TotalCredit,
            Lines = rows.Select(r => new DetailedLine
            {
                TransactionId = r.TransactionId,
                TransactionNumber = r.TransactionNumber,
                Date = DateOnly.FromDateTime(r.Date),
                Description = r.Description,
                ContactName = r.ContactName,
                Reference = r.Reference,
                ReferenceType = r.ReferenceType,
                ReferenceId = r.ReferenceId,
                Debit = r.Debit,
                Credit = r.Credit,
                RunningBalance = r.RunningBalance
            }).ToList()
        };
    }

    // ============================================
    // صورت‌حساب طرف حساب
    // ============================================

    public async Task<ContactReport> GetContactAsync(ContactReportQuery query, CancellationToken cancellationToken = default)
    {
        return await GetContactAsync(RequireTenant(), query, cancellationToken);
    }

    public async Task<ContactReport> GetContactAsync(
        int tenantId, ContactReportQuery query, CancellationToken cancellationToken = default)
    {
        ValidateRange(query.From, query.To);
        if (tenantId <= 0) throw new InvalidOperationException("شرکت معتبر نیست.");

        var contact = await _context.Contacts
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.Id == query.ContactId && c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => new { c.Name, c.Code, c.Mobile, c.Phone, c.ContactType })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("طرف حساب پیدا نشد.");

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId, DbType.Int32);
        parameters.Add("ContactId", query.ContactId, DbType.Int32);
        AddDate(parameters, "FromDate", query.From);
        AddDate(parameters, "ToDate", query.To);
        parameters.Add("OnlyPosted", query.OnlyPosted, DbType.Boolean);

        var (summary, rows) = await QueryAsync<SummaryRow, ContactLineRow>(
            "dbo.usp_Report_Contact", parameters, cancellationToken);

        return new ContactReport
        {
            ContactName = contact.Name,
            ContactCode = contact.Code,
            Mobile = contact.Mobile,
            Phone = contact.Phone,
            ContactType = contact.ContactType,
            OpeningBalance = summary.OpeningBalance,
            TotalDebit = summary.TotalDebit,
            TotalCredit = summary.TotalCredit,
            ClosingBalance = summary.OpeningBalance + summary.TotalDebit - summary.TotalCredit,
            Lines = rows.Select(r => new ContactLine
            {
                TransactionId = r.TransactionId,
                TransactionNumber = r.TransactionNumber,
                Date = DateOnly.FromDateTime(r.Date),
                Description = r.Description,
                Reference = r.Reference,
                ReferenceType = r.ReferenceType,
                ReferenceId = r.ReferenceId,
                AccountName = r.AccountName,
                Debit = r.Debit,
                Credit = r.Credit,
                RunningBalance = r.RunningBalance
            }).ToList()
        };
    }

    // ============================================
    // فاکتورها
    // ============================================

    public async Task<InvoiceReport> GetInvoicesAsync(InvoiceReportQuery query, CancellationToken cancellationToken = default)
    {
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new InvalidOperationException("تاریخ شروع نباید بعد از تاریخ پایان باشد.");

        var tenantId = RequireTenant();

        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId, DbType.Int32);
        parameters.Add("InvoiceType", query.InvoiceType.HasValue ? (int?)query.InvoiceType.Value : null, DbType.Int32);
        parameters.Add("Status", query.Status.HasValue ? (int?)query.Status.Value : null, DbType.Int32);
        parameters.Add("ContactId", query.ContactId is > 0 ? (int?)query.ContactId : null, DbType.Int32);
        AddDate(parameters, "FromDate", query.From);
        AddDate(parameters, "ToDate", query.To);
        parameters.Add("OnlyOpen", query.OnlyOpen, DbType.Boolean);
        parameters.Add("PageNumber", pageNumber, DbType.Int32);
        parameters.Add("PageSize", pageSize, DbType.Int32);

        var (summary, rows) = await QueryAsync<InvoiceSummaryRow, InvoiceRow>(
            "dbo.usp_Report_Invoices", parameters, cancellationToken);

        return new InvoiceReport
        {
            Count = summary.Count,
            TotalAmount = summary.TotalAmount,
            TotalPaid = summary.TotalPaid,
            TotalSettlementDiscount = summary.TotalSettlementDiscount,
            TotalRemaining = summary.TotalRemaining,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Rows = rows.Select(r => new InvoiceReportRow
            {
                Id = r.Id,
                Number = r.Number,
                Date = DateOnly.FromDateTime(r.Date),
                InvoiceType = (InvoiceType)r.InvoiceType,
                Status = (InvoiceStatus)r.Status,
                ContactId = r.ContactId,
                ContactName = r.ContactName,
                Description = r.Description,
                TotalAmount = r.TotalAmount,
                PaidAmount = r.PaidAmount,
                SettlementDiscount = r.SettlementDiscount,
                RemainingAmount = r.RemainingAmount
            }).ToList()
        };
    }

    // ============================================
    // بدهکاران / بستانکاران
    // ============================================

    public async Task<ContactBalancesReport> GetContactBalancesAsync(
        ContactBalancesQuery query, CancellationToken cancellationToken = default)
    {
        if (query.AsOf == default)
            throw new InvalidOperationException("تاریخ گزارش الزامی است.");

        if (!Enum.IsDefined(query.Mode))
            throw new InvalidOperationException("نوع گزارش نامعتبر است.");

        if (query.MinAmount < 0)
            throw new InvalidOperationException("حداقل مبلغ نمی‌تواند منفی باشد.");

        var tenantId = RequireTenant();

        var debtors = query.Mode == ContactBalanceMode.Debtors;
        var pageSize = query.PageSize <= 0 ? 0 : Math.Min(query.PageSize, MaxPageSize);
        var pageNumber = Math.Max(query.PageNumber, 1);

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId, DbType.Int32);
        AddDate(parameters, "AsOfDate", query.AsOf);
        parameters.Add("Mode", (int)query.Mode, DbType.Int32);
        parameters.Add("MinAmount", query.MinAmount, DbType.Decimal);
        parameters.Add("Search", string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(), DbType.String, size: 100);

        // بدهکاران ↔ فاکتور فروش و چک دریافتنی؛ بستانکاران ↔ فاکتور خرید و چک پرداختنی
        parameters.Add("InvoiceType", (int)(debtors ? InvoiceType.Sale : InvoiceType.Purchase), DbType.Int32);
        parameters.Add("InvoiceStatusPosted", (int)InvoiceStatus.Posted, DbType.Int32);
        parameters.Add("ChequeDirection", (int)(debtors ? ChequeDirection.Received : ChequeDirection.Issued), DbType.Int32);
        parameters.Add("ChequeStatusInProgress", (int)ChequeStatus.InProgress, DbType.Int32);
        parameters.Add("ChequeStatusRePresented", (int)ChequeStatus.RePresented, DbType.Int32);

        parameters.Add("SortBy", (int)query.SortBy, DbType.Int32);
        parameters.Add("PageNumber", pageNumber, DbType.Int32);
        parameters.Add("PageSize", pageSize, DbType.Int32);

        var (summary, rows) = await QueryAsync<BalancesSummaryRow, BalanceRow>(
            "dbo.usp_Report_ContactBalances", parameters, cancellationToken);

        return new ContactBalancesReport
        {
            Mode = query.Mode,
            AsOf = query.AsOf,
            Count = summary.Count,
            TotalAmount = summary.TotalAmount,
            TotalDebit = summary.TotalDebit,
            TotalCredit = summary.TotalCredit,
            TotalOpenInvoiceRemaining = summary.TotalOpenInvoiceRemaining,
            TotalAge0To30 = summary.TotalAge0To30,
            TotalAge31To60 = summary.TotalAge31To60,
            TotalAge61To90 = summary.TotalAge61To90,
            TotalAge90Plus = summary.TotalAge90Plus,
            TotalPendingCheque = summary.TotalPendingCheque,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Rows = rows.Select(r => new ContactBalanceRow
            {
                ContactId = r.ContactId,
                ContactName = r.ContactName,
                ContactCode = r.ContactCode,
                Mobile = r.Mobile,
                TotalDebit = r.TotalDebit,
                TotalCredit = r.TotalCredit,
                Amount = r.Amount,
                LastTransactionDate = r.LastTransactionDate.HasValue
                    ? DateOnly.FromDateTime(r.LastTransactionDate.Value)
                    : null,
                OpenInvoiceCount = r.OpenInvoiceCount,
                OpenInvoiceRemaining = r.OpenInvoiceRemaining,
                Age0To30 = r.Age0To30,
                Age31To60 = r.Age31To60,
                Age61To90 = r.Age61To90,
                Age90Plus = r.Age90Plus,
                PendingChequeAmount = r.PendingChequeAmount
            }).ToList()
        };
    }

    // ============================================
    // Helpers
    // ============================================

    private int RequireTenant()
        => _currentTenant.TenantId ?? throw new InvalidOperationException("شرکت فعال مشخص نیست.");

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from == default)
            throw new InvalidOperationException("تاریخ شروع الزامی است.");

        if (to == default)
            throw new InvalidOperationException("تاریخ پایان الزامی است.");

        if (from > to)
            throw new InvalidOperationException("تاریخ شروع نباید بعد از تاریخ پایان باشد.");
    }

    private static void AddDate(DynamicParameters parameters, string name, DateOnly? value)
        => parameters.Add(name, value?.ToDateTime(TimeOnly.MinValue), DbType.Date);

    private async Task<AccountHeader> GetAccountAsync(int accountId, int tenantId, CancellationToken cancellationToken)
    {
        return await _context.Accounts
                   .AsNoTracking()
                   .Where(a => a.Id == accountId && a.TenantId == tenantId)
                   .Select(a => new AccountHeader(a.Name, a.Code, a.AccountType))
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new InvalidOperationException("حساب پیدا نشد.");
    }

    /// <summary>
    /// پروسیجر با دو Result Set (خلاصه + ردیف‌ها). اتصال EF استفاده می‌شود
    /// (اگر بسته بود باز و بعد از اجرا بسته می‌شود؛ اگر تراکنش EF فعال بود در همان اجرا می‌شود).
    /// </summary>
    private async Task<(TSummary Summary, List<TRow> Rows)> QueryAsync<TSummary, TRow>(
        string procedure, DynamicParameters parameters, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;

        if (wasClosed)
            await connection.OpenAsync(cancellationToken);

        try
        {
            var command = new CommandDefinition(
                procedure,
                parameters,
                _context.Database.CurrentTransaction?.GetDbTransaction(),
                commandTimeout: 60,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken);

            using var multi = await connection.QueryMultipleAsync(command);

            var summary = await multi.ReadSingleAsync<TSummary>();
            var rows = (await multi.ReadAsync<TRow>()).ToList();

            return (summary, rows);
        }
        finally
        {
            if (wasClosed)
                await connection.CloseAsync();
        }
    }

    // ============================================
    // ردیف‌های خام (دقیقاً هم‌نام ستون‌های پروسیجر)
    // ============================================

    private sealed record AccountHeader(string Name, string? Code, AccountType AccountType);

    private sealed class SummaryRow
    {
        public decimal OpeningBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public int LineCount { get; set; }
    }

    private sealed class LedgerLineRow
    {
        public int TransactionId { get; set; }
        public int TransactionNumber { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }

    private sealed class DetailedLineRow
    {
        public int TransactionId { get; set; }
        public int TransactionNumber { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public string? ContactName { get; set; }
        public string? Reference { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }

    private sealed class ContactLineRow
    {
        public int TransactionId { get; set; }
        public int TransactionNumber { get; set; }
        public DateTime Date { get; set; }
        public string? Description { get; set; }
        public string? Reference { get; set; }
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public string? AccountName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
    }

    private sealed class BalancesSummaryRow
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalOpenInvoiceRemaining { get; set; }
        public decimal TotalAge0To30 { get; set; }
        public decimal TotalAge31To60 { get; set; }
        public decimal TotalAge61To90 { get; set; }
        public decimal TotalAge90Plus { get; set; }
        public decimal TotalPendingCheque { get; set; }
    }

    private sealed class BalanceRow
    {
        public int ContactId { get; set; }
        public string? ContactName { get; set; }
        public string? ContactCode { get; set; }
        public string? Mobile { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Amount { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public int OpenInvoiceCount { get; set; }
        public decimal OpenInvoiceRemaining { get; set; }
        public decimal Age0To30 { get; set; }
        public decimal Age31To60 { get; set; }
        public decimal Age61To90 { get; set; }
        public decimal Age90Plus { get; set; }
        public decimal PendingChequeAmount { get; set; }
    }

    private sealed class InvoiceSummaryRow
    {
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalSettlementDiscount { get; set; }
        public decimal TotalRemaining { get; set; }
    }

    private sealed class InvoiceRow
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public DateTime Date { get; set; }
        public int InvoiceType { get; set; }
        public int Status { get; set; }
        public int? ContactId { get; set; }
        public string? ContactName { get; set; }
        public string? Description { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal SettlementDiscount { get; set; }
        public decimal RemainingAmount { get; set; }
    }
}
