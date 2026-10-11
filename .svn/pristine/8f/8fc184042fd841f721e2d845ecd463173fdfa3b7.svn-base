using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Areas.Accounting.ViewModels.Report;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Globalization;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public ReportsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // GET: /Accounting/Reports/Ledger
    // GET: /Accounting/Reports/Ledger
    [Authorize(Policy = "Report.Ledger")]
    public async Task<IActionResult> Ledger(
        int? accountId = null,
        string? fromDate = null,
        string? toDate = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        // 🆕 تاریخ پیش‌فرض (سال جاری شمسی)
        var today = DateTime.Now;
        var pc = new PersianCalendar();
        var currentYear = pc.GetYear(today);
        var currentMonth = pc.GetMonth(today);
        var lastDayOfMonth = pc.GetDaysInMonth(currentYear, currentMonth);
        var defaultFrom = $"{currentYear}/{currentMonth:00}/01";   // 🆕 ابتدای ماه
        var defaultTo = $"{currentYear}/{currentMonth:00}/{lastDayOfMonth:00}";   // انتهای ماه

        // 🆕 ساخت model (همیشه)
        var model = new LedgerReportViewModel
        {
            AccountId = accountId,
            FromDate = string.IsNullOrWhiteSpace(fromDate) ? defaultFrom : fromDate,
            ToDate = string.IsNullOrWhiteSpace(toDate) ? defaultTo : toDate,
            AccountOptions = await GetAccountOptionsAsync()
        };

        // 🆕 اگه حساب انتخاب نشده
        if (!accountId.HasValue)
        {
            if (Request.Query.ContainsKey("accountId"))
            {
                TempData["Error"] = "لطفاً یک حساب را انتخاب کنید.";
            }
            return View(model);
        }

        // اطلاعات حساب
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId.Value && a.TenantId == tenantId.Value);

        if (account == null) return NotFound();

        model.AccountName = account.Name;
        model.AccountCode = account.Code;

        // تبدیل تاریخ‌ها
        var from = ParsePersianDate(model.FromDate);
        var to = ParsePersianDate(model.ToDate);

        // دیباگ
        System.Diagnostics.Debug.WriteLine($"FromDate: '{model.FromDate}' → {from}");
        System.Diagnostics.Debug.WriteLine($"ToDate: '{model.ToDate}' → {to}");

        // مانده اول دوره
        if (from.HasValue)
        {
            var openingDebit = await _context.TransactionLines
                .Where(l => l.TenantId == tenantId.Value
                         && l.AccountId == accountId.Value
                         && !l.IsDeleted
                         && l.Transaction.IsPosted
                         && l.Transaction.Date < from.Value)
                .SumAsync(l => (decimal?)l.Debit) ?? 0;

            var openingCredit = await _context.TransactionLines
                .Where(l => l.TenantId == tenantId.Value
                         && l.AccountId == accountId.Value
                         && !l.IsDeleted
                         && l.Transaction.IsPosted
                         && l.Transaction.Date < from.Value)
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            model.OpeningBalance = openingDebit - openingCredit;
        }

        // تراکنش‌ها
        var query = _context.TransactionLines
            .Include(l => l.Transaction)
            .Where(l => l.TenantId == tenantId.Value
                     && l.AccountId == accountId.Value
                     && !l.IsDeleted
                     && l.Transaction.IsPosted);

        if (from.HasValue)
            query = query.Where(l => l.Transaction.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Transaction.Date <= to.Value);

        var lines = await query
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

        // محاسبه مانده تجمعی
        var runningBalance = model.OpeningBalance;
        foreach (var line in lines)
        {
            runningBalance += line.Debit - line.Credit;
            line.RunningBalance = runningBalance;
            line.DateShamsi = ToPersianDateString(line.Date);
        }

        model.Lines = lines;
        model.TotalDebit = lines.Sum(l => l.Debit);
        model.TotalCredit = lines.Sum(l => l.Credit);
        model.ClosingBalance = model.OpeningBalance + model.TotalDebit - model.TotalCredit;

        return View(model);
    }

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

        // 🆕 تاریخ پیش‌فرض
        var today = DateTime.Now;
        var pc = new PersianCalendar();
        var currentYear = pc.GetYear(today);
        var currentMonth = pc.GetMonth(today);
        var lastDayOfMonth = pc.GetDaysInMonth(currentYear, currentMonth);

        var defaultFrom = $"{currentYear}/01/01";
        var defaultTo = $"{currentYear}/{currentMonth:00}/{lastDayOfMonth:00}";

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
            {
                TempData["Error"] = "لطفاً یک حساب را انتخاب کنید.";
            }
            return View(model);
        }

        var account = await _context.Accounts
            .Include(a => a.AccountCategory)
            .FirstOrDefaultAsync(a => a.Id == accountId.Value && a.TenantId == tenantId.Value);

        if (account == null) return NotFound();

        model.AccountName = account.Name;
        model.AccountCode = account.Code;
        model.AccountTypeName = GetAccountTypeName(account.AccountType);

        // 🆕 تبدیل تاریخ
        var from = ParsePersianDate(model.FromDate);
        var to = ParsePersianDate(model.ToDate);

        // 🆕 مانده اول دوره
        if (from.HasValue)
        {
            var openingQuery = _context.TransactionLines
                .Include(l => l.Transaction)
                .Where(l => l.TenantId == tenantId.Value
                         && l.AccountId == accountId.Value
                         && !l.IsDeleted
                         && !l.Transaction.IsDeleted
                         && l.Transaction.Date < from.Value);

            if (onlyPosted)
                openingQuery = openingQuery.Where(l => l.Transaction.IsPosted);

            var openingDebit = await openingQuery.SumAsync(l => (decimal?)l.Debit) ?? 0;
            var openingCredit = await openingQuery.SumAsync(l => (decimal?)l.Credit) ?? 0;

            model.OpeningBalance = openingDebit - openingCredit;
        }

        // 🆕 خطوط
        var query = _context.TransactionLines
            .Include(l => l.Transaction)
            .Include(l => l.Contact)
            .Where(l => l.TenantId == tenantId.Value
                     && l.AccountId == accountId.Value
                     && !l.IsDeleted
                     && !l.Transaction.IsDeleted);

        if (from.HasValue)
            query = query.Where(l => l.Transaction.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Transaction.Date <= to.Value);

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

        // 🆕 محاسبه مانده
        var runningBalance = model.OpeningBalance;
        foreach (var line in lines)
        {
            runningBalance += line.Debit - line.Credit;
            line.RunningBalance = runningBalance;
            line.DateShamsi = ToPersianDateString(line.Date);
        }

        model.Lines = lines;
        model.TotalDebit = lines.Sum(l => l.Debit);
        model.TotalCredit = lines.Sum(l => l.Credit);
        model.ClosingBalance = model.OpeningBalance + model.TotalDebit - model.TotalCredit;

        return View(model);
    }

    // ---------- Helpers ----------

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

    private string GetAccountTypeName(AccountType type)
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

        // تاریخ پیش‌فرض
        var today = DateTime.Now;
        var pc = new PersianCalendar();
        var currentYear = pc.GetYear(today);
        var currentMonth = pc.GetMonth(today);
        var lastDayOfMonth = pc.GetDaysInMonth(currentYear, currentMonth);

        var defaultFrom = $"{currentYear}/01/01";
        var defaultTo = $"{currentYear}/{currentMonth:00}/{lastDayOfMonth:00}";

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
            {
                TempData["Error"] = "لطفاً یک طرف حساب را انتخاب کنید.";
            }
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

        // تبدیل تاریخ‌ها
        var from = ParsePersianDate(model.FromDate);
        var to = ParsePersianDate(model.ToDate);

        // 🆕 مانده اول دوره
        if (from.HasValue)
        {
            var openingQuery = _context.TransactionLines
                .Include(l => l.Transaction)
                .Where(l => l.TenantId == tenantId.Value
                         && l.ContactId == contactId.Value
                         && !l.IsDeleted
                         && !l.Transaction.IsDeleted
                         && l.Transaction.Date < from.Value);

            if (onlyPosted)
                openingQuery = openingQuery.Where(l => l.Transaction.IsPosted);

            var openingDebit = await openingQuery.SumAsync(l => (decimal?)l.Debit) ?? 0;
            var openingCredit = await openingQuery.SumAsync(l => (decimal?)l.Credit) ?? 0;

            model.OpeningBalance = openingDebit - openingCredit;
        }

        // 🆕 خطوط
        var query = _context.TransactionLines
            .Include(l => l.Transaction)
            .Include(l => l.Account)
            .Where(l => l.TenantId == tenantId.Value
                     && l.ContactId == contactId.Value
                     && !l.IsDeleted
                     && !l.Transaction.IsDeleted);

        if (from.HasValue)
            query = query.Where(l => l.Transaction.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Transaction.Date <= to.Value);

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

        // محاسبه مانده تجمعی
        var runningBalance = model.OpeningBalance;
        foreach (var line in lines)
        {
            runningBalance += line.Debit - line.Credit;
            line.RunningBalance = runningBalance;
            line.DateShamsi = ToPersianDateString(line.Date);
        }

        model.Lines = lines;
        model.TotalDebit = lines.Sum(l => l.Debit);
        model.TotalCredit = lines.Sum(l => l.Credit);
        model.ClosingBalance = model.OpeningBalance + model.TotalDebit - model.TotalCredit;

        return View(model);
    }
    // ---------- Helpers ----------

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

    private DateTime? ParsePersianDate(string? persianDate)
    {
        if (string.IsNullOrWhiteSpace(persianDate))
            return null;

        try
        {
            persianDate = ToEnglishNumber(persianDate);
            var parts = persianDate.Split('/', '-');
            if (parts.Length != 3) return null;

            var year = int.Parse(parts[0]);
            var month = int.Parse(parts[1]);
            var day = int.Parse(parts[2]);

            var pc = new PersianCalendar();
            return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch
        {
            return null;
        }
    }

    private string ToPersianDateString(DateTime date)
    {
        var pc = new PersianCalendar();
        return $"{pc.GetYear(date):0000}/{pc.GetMonth(date):00}/{pc.GetDayOfMonth(date):00}";
    }

    private string ToEnglishNumber(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("۰", "0").Replace("۱", "1").Replace("۲", "2")
            .Replace("۳", "3").Replace("۴", "4").Replace("۵", "5")
            .Replace("۶", "6").Replace("۷", "7").Replace("۸", "8")
            .Replace("۹", "9")
            .Replace("٠", "0").Replace("١", "1").Replace("٢", "2")
            .Replace("٣", "3").Replace("٤", "4").Replace("٥", "5")
            .Replace("٦", "6").Replace("٧", "7").Replace("٨", "8")
            .Replace("٩", "9");
    }
}