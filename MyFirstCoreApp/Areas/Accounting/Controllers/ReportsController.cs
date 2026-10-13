using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Areas.Accounting.ViewModels.Report;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class ReportsController : Controller
{
    private const string InvalidRangeMessage = "تاریخ شروع یا پایان نامعتبر است. نمونه‌ی درست: 1405/01/10";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly TimeProvider _timeProvider;

    public ReportsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentTenant = currentTenant;
        _timeProvider = timeProvider;
    }

    // ============================================
    // گزارش معین
    // ============================================

    // GET: /Accounting/Reports/Ledger
    [Authorize(Policy = "Report.Ledger")]
    public async Task<IActionResult> Ledger(
        int? accountId = null,
        string? fromDate = null,
        string? toDate = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        // پیش‌فرض: ماه جاری شمسی
        var (defaultFrom, defaultTo) = CurrentMonthRange();

        var model = new LedgerReportViewModel
        {
            AccountId = accountId,
            FromDate = string.IsNullOrWhiteSpace(fromDate) ? defaultFrom : fromDate,
            ToDate = string.IsNullOrWhiteSpace(toDate) ? defaultTo : toDate,
            AccountOptions = await GetAccountOptionsAsync()
        };

        if (!accountId.HasValue)
        {
            if (Request.Query.ContainsKey("accountId"))
                TempData["Error"] = "لطفاً یک حساب را انتخاب کنید.";

            return View(model);
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId.Value && a.TenantId == tenantId.Value);

        if (account == null) return NotFound();

        model.AccountName = account.Name;
        model.AccountCode = account.Code;

        if (!TryParseRange(model.FromDate, model.ToDate, out var from, out var to))
            return View(model);

        // مانده اول دوره
        var openingQuery = _context.TransactionLines
            .Where(l => l.TenantId == tenantId.Value
                     && l.AccountId == accountId.Value
                     && !l.IsDeleted
                     && l.Transaction.IsPosted
                     && l.Transaction.Date < from);

        var openingDebit = await openingQuery.SumAsync(l => (decimal?)l.Debit) ?? 0;
        var openingCredit = await openingQuery.SumAsync(l => (decimal?)l.Credit) ?? 0;
        model.OpeningBalance = openingDebit - openingCredit;

        // تراکنش‌های بازه (هر دو سر بازه شامل می‌شوند)
        var lines = await _context.TransactionLines
            .Where(l => l.TenantId == tenantId.Value
                     && l.AccountId == accountId.Value
                     && !l.IsDeleted
                     && l.Transaction.IsPosted
                     && l.Transaction.Date >= from
                     && l.Transaction.Date <= to)
            .OrderBy(l => l.Transaction.Date)
            .ThenBy(l => l.Transaction.Number)
            .ThenBy(l => l.LineNumber)
            .Select(l => new LedgerReportLine
            {
                TransactionId = l.TransactionId,
                TransactionNumber = l.Transaction.Number,
                Date = l.Transaction.Date,
                Description = l.Description ?? l.Transaction.Description,
                Reference = l.Transaction.Reference,
                Debit = l.Debit,
                Credit = l.Credit
            })
            .ToListAsync();

        var runningBalance = model.OpeningBalance;
        foreach (var line in lines)
        {
            runningBalance += line.Debit - line.Credit;
            line.RunningBalance = runningBalance;
            line.DateShamsi = PersianDate.Format(line.Date);
        }

        model.Lines = lines;
        model.TotalDebit = lines.Sum(l => l.Debit);
        model.TotalCredit = lines.Sum(l => l.Credit);
        model.ClosingBalance = model.OpeningBalance + model.TotalDebit - model.TotalCredit;

        return View(model);
    }

    // ============================================
    // گزارش تفصیلی
    // ============================================

    // GET: /Accounting/Reports/Detailed
    [Authorize(Policy = "Report.Ledger")]
    public async Task<IActionResult> Detailed(
        int? accountId = null,
        int? contactId = null,
        string? fromDate = null,
        string? toDate = null,
        bool onlyPosted = true)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        // پیش‌فرض: ابتدای سال شمسی تا پایان ماه جاری
        var (defaultFrom, defaultTo) = YearToDateRange();

        var model = new DetailedReportViewModel
        {
            AccountId = accountId,
            ContactId = contactId,
            FromDate = string.IsNullOrWhiteSpace(fromDate) ? defaultFrom : fromDate,
            ToDate = string.IsNullOrWhiteSpace(toDate) ? defaultTo : toDate,
            OnlyPosted = onlyPosted,
            AccountOptions = await GetAccountOptionsAsync(),
            ContactOptions = await GetContactOptionsAsync()
        };

        if (!accountId.HasValue)
        {
            if (Request.Query.ContainsKey("accountId"))
                TempData["Error"] = "لطفاً یک حساب را انتخاب کنید.";

            return View(model);
        }

        var account = await _context.Accounts
            .Include(a => a.AccountCategory)
            .FirstOrDefaultAsync(a => a.Id == accountId.Value && a.TenantId == tenantId.Value);

        if (account == null) return NotFound();

        model.AccountName = account.Name;
        model.AccountCode = account.Code;
        model.AccountTypeName = GetAccountTypeName(account.AccountType);

        if (!TryParseRange(model.FromDate, model.ToDate, out var from, out var to))
            return View(model);

        // مانده اول دوره
        var openingQuery = _context.TransactionLines
            .Where(l => l.TenantId == tenantId.Value
                     && l.AccountId == accountId.Value
                     && !l.IsDeleted
                     && !l.Transaction.IsDeleted
                     && l.Transaction.Date < from);

        if (onlyPosted)
            openingQuery = openingQuery.Where(l => l.Transaction.IsPosted);

        var openingDebit = await openingQuery.SumAsync(l => (decimal?)l.Debit) ?? 0;
        var openingCredit = await openingQuery.SumAsync(l => (decimal?)l.Credit) ?? 0;
        model.OpeningBalance = openingDebit - openingCredit;

        // خطوط بازه
        var query = _context.TransactionLines
            .Where(l => l.TenantId == tenantId.Value
                     && l.AccountId == accountId.Value
                     && !l.IsDeleted
                     && !l.Transaction.IsDeleted
                     && l.Transaction.Date >= from
                     && l.Transaction.Date <= to);

        if (contactId.HasValue)
            query = query.Where(l => l.ContactId == contactId.Value);

        if (onlyPosted)
            query = query.Where(l => l.Transaction.IsPosted);

        var lines = await query
            .OrderBy(l => l.Transaction.Date)
            .ThenBy(l => l.Transaction.Number)
            .ThenBy(l => l.LineNumber)
            .Select(l => new DetailedReportLine
            {
                TransactionId = l.TransactionId,
                TransactionNumber = l.Transaction.Number,
                Date = l.Transaction.Date,
                Description = l.Description ?? l.Transaction.Description,
                ContactName = l.Contact != null ? l.Contact.Name : null,
                Reference = l.Transaction.Reference,
                ReferenceType = l.Transaction.ReferenceType,
                ReferenceId = l.Transaction.ReferenceId,
                Debit = l.Debit,
                Credit = l.Credit
            })
            .ToListAsync();

        var runningBalance = model.OpeningBalance;
        foreach (var line in lines)
        {
            runningBalance += line.Debit - line.Credit;
            line.RunningBalance = runningBalance;
            line.DateShamsi = PersianDate.Format(line.Date);
        }

        model.Lines = lines;
        model.TotalDebit = lines.Sum(l => l.Debit);
        model.TotalCredit = lines.Sum(l => l.Credit);
        model.ClosingBalance = model.OpeningBalance + model.TotalDebit - model.TotalCredit;

        return View(model);
    }

    // ============================================
    // گزارش طرف حساب
    // ============================================

    // GET: /Accounting/Reports/Contact
    [Authorize(Policy = "Report.Ledger")]
    public async Task<IActionResult> Contact(
        int? contactId = null,
        string? fromDate = null,
        string? toDate = null,
        bool onlyPosted = true)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var (defaultFrom, defaultTo) = YearToDateRange();

        var model = new ContactReportViewModel
        {
            ContactId = contactId,
            FromDate = string.IsNullOrWhiteSpace(fromDate) ? defaultFrom : fromDate,
            ToDate = string.IsNullOrWhiteSpace(toDate) ? defaultTo : toDate,
            OnlyPosted = onlyPosted,
            ContactOptions = await GetContactOptionsAsync()
        };

        if (!contactId.HasValue)
        {
            if (Request.Query.ContainsKey("contactId"))
                TempData["Error"] = "لطفاً یک طرف حساب را انتخاب کنید.";

            return View(model);
        }

        var contact = await _context.Contacts
            .FirstOrDefaultAsync(c => c.Id == contactId.Value && c.TenantId == tenantId.Value);

        if (contact == null) return NotFound();

        model.ContactName = contact.Name;
        model.ContactCode = contact.Code;
        model.Mobile = contact.Mobile;
        model.Phone = contact.Phone;
        model.ContactType = contact.ContactType;
        model.ContactTypeName = contact.ContactType switch
        {
            ContactType.Customer => "مشتری",
            ContactType.Supplier => "فروشنده",
            ContactType.Both => "مشتری و فروشنده",
            _ => "-"
        };

        if (!TryParseRange(model.FromDate, model.ToDate, out var from, out var to))
            return View(model);

        // مانده اول دوره
        var openingQuery = _context.TransactionLines
            .Where(l => l.TenantId == tenantId.Value
                     && l.ContactId == contactId.Value
                     && !l.IsDeleted
                     && !l.Transaction.IsDeleted
                     && l.Transaction.Date < from);

        if (onlyPosted)
            openingQuery = openingQuery.Where(l => l.Transaction.IsPosted);

        var openingDebit = await openingQuery.SumAsync(l => (decimal?)l.Debit) ?? 0;
        var openingCredit = await openingQuery.SumAsync(l => (decimal?)l.Credit) ?? 0;
        model.OpeningBalance = openingDebit - openingCredit;

        // خطوط بازه
        var query = _context.TransactionLines
            .Where(l => l.TenantId == tenantId.Value
                     && l.ContactId == contactId.Value
                     && !l.IsDeleted
                     && !l.Transaction.IsDeleted
                     && l.Transaction.Date >= from
                     && l.Transaction.Date <= to);

        if (onlyPosted)
            query = query.Where(l => l.Transaction.IsPosted);

        var lines = await query
            .OrderBy(l => l.Transaction.Date)
            .ThenBy(l => l.Transaction.Number)
            .ThenBy(l => l.LineNumber)
            .Select(l => new ContactReportLine
            {
                TransactionId = l.TransactionId,
                TransactionNumber = l.Transaction.Number,
                Date = l.Transaction.Date,
                Description = l.Description ?? l.Transaction.Description,
                Reference = l.Transaction.Reference,
                ReferenceType = l.Transaction.ReferenceType,
                ReferenceId = l.Transaction.ReferenceId,
                AccountName = l.Account != null ? l.Account.Name : null,
                Debit = l.Debit,
                Credit = l.Credit
            })
            .ToListAsync();

        var runningBalance = model.OpeningBalance;
        foreach (var line in lines)
        {
            runningBalance += line.Debit - line.Credit;
            line.RunningBalance = runningBalance;
            line.DateShamsi = PersianDate.Format(line.Date);
        }

        model.Lines = lines;
        model.TotalDebit = lines.Sum(l => l.Debit);
        model.TotalCredit = lines.Sum(l => l.Credit);
        model.ClosingBalance = model.OpeningBalance + model.TotalDebit - model.TotalCredit;

        return View(model);
    }

    // ============================================
    // Helpers
    // ============================================

    /// <summary>
    /// تاریخ نامعتبر دیگر بی‌صدا نادیده گرفته نمی‌شود (قبلاً گزارش بدون محدودیت تاریخ می‌ساخت
    /// و کاربر متوجه نمی‌شد). پیام خطا در TempData قرار می‌گیرد.
    /// </summary>
    private bool TryParseRange(string? fromText, string? toText, out DateOnly from, out DateOnly to)
    {
        var fromOk = PersianDate.TryParse(fromText, out from);
        var toOk = PersianDate.TryParse(toText, out to);

        if (!fromOk || !toOk)
        {
            TempData["Error"] = InvalidRangeMessage;
            return false;
        }

        if (from > to)
        {
            TempData["Error"] = "تاریخ شروع نباید بعد از تاریخ پایان باشد.";
            return false;
        }

        return true;
    }

    private (string From, string To) CurrentMonthRange()
    {
        var today = _timeProvider.Today();
        return (PersianDate.Format(PersianDate.StartOfMonth(today)),
                PersianDate.Format(PersianDate.EndOfMonth(today)));
    }

    private (string From, string To) YearToDateRange()
    {
        var today = _timeProvider.Today();
        return (PersianDate.Format(PersianDate.StartOfYear(today)),
                PersianDate.Format(PersianDate.EndOfMonth(today)));
    }

    private static string GetAccountTypeName(AccountType type)
    {
        return type switch
        {
            AccountType.Bank => "بانکی",
            AccountType.Cash => "نقدی",
            AccountType.Receivable => "دریافتنی",
            AccountType.Payable => "پرداختنی",
            AccountType.Capital => "سرمایه",
            AccountType.Income => "درآمد",
            AccountType.Expense => "هزینه",
            AccountType.Contra => "واسط",
            _ => "-"
        };
    }

    private async Task<List<SelectListItem>> GetAccountOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Accounts
            .Where(a => a.TenantId == tenantId.Value && a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = $"{a.Name} {(a.Code != null ? $"({a.Code})" : "")}"
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetContactOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Contacts
            .Where(c => c.TenantId == tenantId.Value && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }
}
