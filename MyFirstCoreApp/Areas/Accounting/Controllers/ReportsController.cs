using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Reports;
using MyFirstCoreApp.Areas.Accounting.ViewModels.Report;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Globalization;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

/// <summary>
/// کنترلر فقط: ورودی فرم را می‌خواند و می‌سنجد، سرویس گزارش را صدا می‌زند و نتیجه را به ViewModel می‌برد.
/// هیچ کوئری گزارشی اینجا نیست (همه در Stored Procedureها).
/// </summary>
[Area("Accounting")]
[Authorize]
public class ReportsController : Controller
{
    private const string InvalidRangeMessage = "تاریخ شروع یا پایان نامعتبر است. نمونه‌ی درست: 1405/01/10";
    private const string UnexpectedErrorMessage = "خطای غیرمنتظره در تهیه‌ی گزارش رخ داد. لطفاً دوباره تلاش کنید.";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IReportService _reportService;
    private readonly IFiscalYearService _fiscalYearService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IReportService reportService,
        IFiscalYearService fiscalYearService,
        TimeProvider timeProvider,
        ILogger<ReportsController> logger)
    {
        _context = context;
        _currentTenant = currentTenant;
        _reportService = reportService;
        _fiscalYearService = fiscalYearService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ============================================
    // گزارش معین
    // ============================================

    [Authorize(Policy = "Report.Ledger")]
    public async Task<IActionResult> Ledger(int? accountId = null, string? fromDate = null, string? toDate = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        // پیش‌فرض: ماه جاری شمسی
        var today = _timeProvider.Today();

        var model = new LedgerReportViewModel
        {
            AccountId = accountId,
            FromDate = DefaultOrGiven(fromDate, PersianDate.StartOfMonth(today)),
            ToDate = DefaultOrGiven(toDate, PersianDate.EndOfMonth(today)),
            AccountOptions = await GetAccountOptionsAsync()
        };

        if (!accountId.HasValue)
        {
            if (Request.Query.ContainsKey("accountId"))
                TempData["Error"] = "لطفاً یک حساب را انتخاب کنید.";

            return View(model);
        }

        if (!TryParseRange(model.FromDate, model.ToDate, out var from, out var to))
            return View(model);

        try
        {
            var report = await _reportService.GetLedgerAsync(
                new LedgerReportQuery { AccountId = accountId.Value, From = from, To = to });

            model.AccountName = report.AccountName;
            model.AccountCode = report.AccountCode;
            model.OpeningBalance = report.OpeningBalance;
            model.TotalDebit = report.TotalDebit;
            model.TotalCredit = report.TotalCredit;
            model.ClosingBalance = report.ClosingBalance;
            model.Lines = report.Lines.Select(l => new LedgerReportLine
            {
                TransactionId = l.TransactionId,
                TransactionNumber = l.TransactionNumber,
                Date = l.Date,
                DateShamsi = PersianDate.Format(l.Date),
                Description = l.Description,
                Reference = l.Reference,
                Debit = l.Debit,
                Credit = l.Credit,
                RunningBalance = l.RunningBalance
            }).ToList();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ledger report failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return View(model);
    }

    // ============================================
    // گزارش تفصیلی
    // ============================================

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

        var (yearStart, yearEnd) = await GetFiscalYearRangeAsync(tenantId.Value);

        var model = new DetailedReportViewModel
        {
            AccountId = accountId,
            ContactId = contactId,
            FromDate = DefaultOrGiven(fromDate, yearStart),
            ToDate = DefaultOrGiven(toDate, yearEnd),
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

        if (!TryParseRange(model.FromDate, model.ToDate, out var from, out var to))
            return View(model);

        try
        {
            var report = await _reportService.GetDetailedAsync(new DetailedReportQuery
            {
                AccountId = accountId.Value,
                ContactId = contactId,
                From = from,
                To = to,
                OnlyPosted = onlyPosted
            });

            model.AccountName = report.AccountName;
            model.AccountCode = report.AccountCode;
            model.AccountTypeName = GetAccountTypeName(report.AccountType);
            model.OpeningBalance = report.OpeningBalance;
            model.TotalDebit = report.TotalDebit;
            model.TotalCredit = report.TotalCredit;
            model.ClosingBalance = report.ClosingBalance;
            model.Lines = report.Lines.Select(l => new DetailedReportLine
            {
                TransactionId = l.TransactionId,
                TransactionNumber = l.TransactionNumber,
                Date = l.Date,
                DateShamsi = PersianDate.Format(l.Date),
                Description = l.Description,
                ContactName = l.ContactName,
                Reference = l.Reference,
                ReferenceType = l.ReferenceType,
                ReferenceId = l.ReferenceId,
                Debit = l.Debit,
                Credit = l.Credit,
                RunningBalance = l.RunningBalance
            }).ToList();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detailed report failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return View(model);
    }

    // ============================================
    // گزارش طرف حساب
    // ============================================

    [Authorize(Policy = "Report.Ledger")]
    public async Task<IActionResult> Contact(
        int? contactId = null,
        string? fromDate = null,
        string? toDate = null,
        bool onlyPosted = true)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var (yearStart, yearEnd) = await GetFiscalYearRangeAsync(tenantId.Value);

        var model = new ContactReportViewModel
        {
            ContactId = contactId,
            FromDate = DefaultOrGiven(fromDate, yearStart),
            ToDate = DefaultOrGiven(toDate, yearEnd),
            OnlyPosted = onlyPosted,
            ContactOptions = await GetContactOptionsAsync()
        };

        if (!contactId.HasValue)
        {
            if (Request.Query.ContainsKey("contactId"))
                TempData["Error"] = "لطفاً یک طرف حساب را انتخاب کنید.";

            return View(model);
        }

        if (!TryParseRange(model.FromDate, model.ToDate, out var from, out var to))
            return View(model);

        try
        {
            var report = await _reportService.GetContactAsync(new ContactReportQuery
            {
                ContactId = contactId.Value,
                From = from,
                To = to,
                OnlyPosted = onlyPosted
            });

            model.ContactName = report.ContactName;
            model.ContactCode = report.ContactCode;
            model.Mobile = report.Mobile;
            model.Phone = report.Phone;
            model.ContactType = report.ContactType;
            model.ContactTypeName = report.ContactType switch
            {
                ContactType.Customer => "مشتری",
                ContactType.Supplier => "فروشنده",
                ContactType.Both => "مشتری و فروشنده",
                _ => "-"
            };
            model.OpeningBalance = report.OpeningBalance;
            model.TotalDebit = report.TotalDebit;
            model.TotalCredit = report.TotalCredit;
            model.ClosingBalance = report.ClosingBalance;
            model.Lines = report.Lines.Select(l => new ContactReportLine
            {
                TransactionId = l.TransactionId,
                TransactionNumber = l.TransactionNumber,
                Date = l.Date,
                DateShamsi = PersianDate.Format(l.Date),
                Description = l.Description,
                Reference = l.Reference,
                ReferenceType = l.ReferenceType,
                ReferenceId = l.ReferenceId,
                AccountName = l.AccountName,
                Debit = l.Debit,
                Credit = l.Credit,
                RunningBalance = l.RunningBalance
            }).ToList();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact report failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return View(model);
    }

    // ============================================
    // گزارش فاکتورها
    // ============================================

    [Authorize(Policy = "Report.Invoice")]
    public async Task<IActionResult> Invoices(
        int? invoiceType = null,
        int? status = null,
        int? contactId = null,
        string? fromDate = null,
        string? toDate = null,
        bool onlyOpen = false,
        int page = 1)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var (yearStart, yearEnd) = await GetFiscalYearRangeAsync(tenantId.Value);

        var model = new InvoiceReportViewModel
        {
            InvoiceType = invoiceType,
            Status = status,
            ContactId = contactId,
            FromDate = DefaultOrGiven(fromDate, yearStart),
            ToDate = DefaultOrGiven(toDate, yearEnd),
            OnlyOpen = onlyOpen,
            Page = Math.Max(page, 1),
            ContactOptions = await GetContactOptionsAsync(),
            InvoiceTypeOptions = GetInvoiceTypeOptions(),
            StatusOptions = GetInvoiceStatusOptions()
        };

        if (!TryParseRange(model.FromDate, model.ToDate, out var from, out var to))
            return View(model);

        try
        {
            var report = await _reportService.GetInvoicesAsync(new InvoiceReportQuery
            {
                InvoiceType = ParseEnum<InvoiceType>(invoiceType),
                Status = ParseEnum<InvoiceStatus>(status),
                ContactId = contactId,
                From = from,
                To = to,
                OnlyOpen = onlyOpen,
                PageNumber = model.Page,
                PageSize = model.PageSize
            });

            model.Count = report.Count;
            model.TotalAmount = report.TotalAmount;
            model.TotalPaid = report.TotalPaid;
            model.TotalSettlementDiscount = report.TotalSettlementDiscount;
            model.TotalRemaining = report.TotalRemaining;
            model.TotalPages = report.TotalPages;
            model.Rows = report.Rows.Select(r => new InvoiceReportRowViewModel
            {
                Id = r.Id,
                Number = r.Number,
                DateShamsi = PersianDate.Format(r.Date),
                InvoiceTypeName = GetInvoiceTypeName(r.InvoiceType),
                StatusName = GetInvoiceStatusName(r.Status),
                ContactName = r.ContactName,
                TotalAmount = r.TotalAmount,
                PaidAmount = r.PaidAmount,
                SettlementDiscount = r.SettlementDiscount,
                RemainingAmount = r.RemainingAmount
            }).ToList();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice report failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return View(model);
    }

    // ============================================
    // بدهکاران و بستانکاران
    // ============================================

    [Authorize(Policy = "Report.Debtors")]
    public Task<IActionResult> Debtors(
        string? asOf = null, string? minAmount = null, string? search = null, int sort = 1, int page = 1)
        => BalancesAsync(ContactBalanceMode.Debtors, asOf, minAmount, search, sort, page);

    [Authorize(Policy = "Report.Debtors")]
    public Task<IActionResult> Creditors(
        string? asOf = null, string? minAmount = null, string? search = null, int sort = 1, int page = 1)
        => BalancesAsync(ContactBalanceMode.Creditors, asOf, minAmount, search, sort, page);

    private async Task<IActionResult> BalancesAsync(
        ContactBalanceMode mode, string? asOf, string? minAmountText, string? search, int sort, int page)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var today = _timeProvider.Today();

        var model = new ContactBalancesViewModel
        {
            Mode = mode,
            AsOf = DefaultOrGiven(asOf, today),
            MinAmountText = minAmountText,
            Search = search,
            Sort = Enum.IsDefined(typeof(ContactBalanceSort), sort) ? sort : (int)ContactBalanceSort.LargestBalance,
            Page = Math.Max(page, 1)
        };

        if (!PersianDate.TryParse(model.AsOf, out var asOfDate))
        {
            TempData["Error"] = "تاریخ نامعتبر است. نمونه‌ی درست: 1405/01/10";
            return View("Balances", model);
        }

        if (!TryParseOptionalAmount(minAmountText, out var minAmount))
        {
            TempData["Error"] = "حداقل مبلغ نامعتبر است. فقط عدد صحیح (ریال) وارد کنید.";
            return View("Balances", model);
        }

        model.AsOfIsPast = asOfDate < today;

        try
        {
            model.Report = await _reportService.GetContactBalancesAsync(new ContactBalancesQuery
            {
                Mode = mode,
                AsOf = asOfDate,
                MinAmount = minAmount,
                Search = search,
                SortBy = (ContactBalanceSort)model.Sort,
                PageNumber = model.Page,
                PageSize = model.PageSize
            });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact balances report failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return View("Balances", model);
    }

    /// <summary>خالی = بدون حداقل؛ پر ولی نامعتبر = خطا. ارقام فارسی و جداکننده‌ی هزارگان مجاز است.</summary>
    private static bool TryParseOptionalAmount(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return true;

        var cleaned = PersianDate.NormalizeDigits(text)
            .Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "");

        return decimal.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
    }

    // ============================================
    // Helpers
    // ============================================

    private static string DefaultOrGiven(string? given, DateOnly defaultDate)
        => string.IsNullOrWhiteSpace(given) ? PersianDate.Format(defaultDate) : given;

    /// <summary>
    /// بازه‌ی پیش‌فرض گزارش‌های دوره‌ای = سال مالیِ جاری شرکت (اگر سالی تعریف نشده بود، سال شمسی جاری).
    /// </summary>
    private async Task<(DateOnly Start, DateOnly End)> GetFiscalYearRangeAsync(int tenantId)
    {
        var year = await _fiscalYearService.GetCurrentAsync(tenantId);
        if (year != null)
            return (year.StartDate, year.EndDate);

        var today = _timeProvider.Today();
        return (PersianDate.StartOfYear(today), PersianDate.EndOfYear(today));
    }

    /// <summary>
    /// تاریخ نامعتبر دیگر بی‌صدا نادیده گرفته نمی‌شود؛ پیام خطا در TempData قرار می‌گیرد.
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

    private static TEnum? ParseEnum<TEnum>(int? value) where TEnum : struct, Enum
        => value.HasValue && Enum.IsDefined(typeof(TEnum), value.Value)
            ? (TEnum)(object)value.Value
            : null;

    private static string GetAccountTypeName(AccountType type) => type switch
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

    private static string GetInvoiceTypeName(InvoiceType type) => type switch
    {
        InvoiceType.Sale => "فروش",
        InvoiceType.Purchase => "خرید",
        _ => "پیش‌فاکتور"
    };

    private static string GetInvoiceStatusName(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "پیش‌نویس",
        InvoiceStatus.Sent => "ارسال شده",
        InvoiceStatus.Revised => "اصلاح شده",
        InvoiceStatus.Approved => "تایید شده",
        InvoiceStatus.Rejected => "رد شده",
        InvoiceStatus.Postponed => "به تعویق",
        InvoiceStatus.Converted => "تبدیل شده",
        InvoiceStatus.Cancelled => "لغو شده",
        InvoiceStatus.Posted => "قطعی",
        _ => "نامشخص"
    };

    private static List<SelectListItem> GetInvoiceTypeOptions() => new()
    {
        new() { Value = ((int)InvoiceType.PreInvoice).ToString(), Text = "پیش‌فاکتور" },
        new() { Value = ((int)InvoiceType.Sale).ToString(), Text = "فروش" },
        new() { Value = ((int)InvoiceType.Purchase).ToString(), Text = "خرید" }
    };

    private static List<SelectListItem> GetInvoiceStatusOptions()
        => Enum.GetValues<InvoiceStatus>()
            .Select(s => new SelectListItem { Value = ((int)s).ToString(), Text = GetInvoiceStatusName(s) })
            .ToList();

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
                Text = a.Code != null ? a.Name + " (" + a.Code + ")" : a.Name
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
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
    }
}
