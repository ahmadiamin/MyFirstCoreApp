using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Banking;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Banking.ViewModels.Cheque;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Banking.Controllers;

[Area("Banking")]
[Authorize]
public class ChequesController : Controller
{
    private const int MaxPageSize = 200;
    private const string NoTenantMessage = "شرکت فعال مشخص نیست.";
    private const string InvalidDateMessage = "تاریخ نامعتبر است. نمونه‌ی درست: 1405/01/10";
    private const string UnexpectedErrorMessage = "خطای غیرمنتظره رخ داد. لطفاً دوباره تلاش کنید.";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IChequeService _chequeService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ChequesController> _logger;

    public ChequesController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IChequeService chequeService,
        TimeProvider timeProvider,
        ILogger<ChequesController> logger)
    {
        _context = context;
        _currentTenant = currentTenant;
        _chequeService = chequeService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ============================================
    // Index
    // ============================================

    [Authorize(Policy = "Cheque.View")]
    public IActionResult Index(ChequeDirection? direction = null)
    {
        ViewBag.Direction = direction;
        return View();
    }

    [HttpPost]
    [Authorize(Policy = "Cheque.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request, [FromQuery] ChequeDirection? direction = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Cheques
            .Where(x => x.TenantId == tenantId.Value);

        if (direction.HasValue)
            query = query.Where(x => x.Direction == direction.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // شماره چک را ممکن است با ارقام فارسی جستجو کنند
            var s = PersianDate.NormalizeDigits(request.Search.Trim());
            query = query.Where(x =>
                x.ChequeNumber.Contains(s) ||
                (x.BankName != null && x.BankName.Contains(s)) ||
                (x.DrawerName != null && x.DrawerName.Contains(s)) ||
                (x.Contact != null && x.Contact.Name.Contains(s)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "chequeNumber" => request.SortDir == "asc" ? query.OrderBy(x => x.ChequeNumber) : query.OrderByDescending(x => x.ChequeNumber),
            "dueDate" => request.SortDir == "asc" ? query.OrderBy(x => x.DueDate) : query.OrderByDescending(x => x.DueDate),
            "amount" => request.SortDir == "asc" ? query.OrderBy(x => x.Amount) : query.OrderByDescending(x => x.Amount),
            _ => query.OrderByDescending(x => x.DueDate)
        };

        var startRow = Math.Max(request.StartRow, 0);
        var pageSize = Math.Clamp(request.EndRow - request.StartRow, 1, MaxPageSize);

        var rows = await query
            .Skip(startRow)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.ChequeNumber,
                x.Direction,
                ContactName = x.Contact != null ? x.Contact.Name : null,
                x.DrawerName,
                x.BankName,
                x.Amount,
                x.DueDate,
                x.Status,
                x.NextFollowUpDate
            })
            .ToListAsync();

        // تاریخ شمسی سمت سرور؛ تاریخ ISO فقط برای محاسبه‌ی «چند روز مانده» در مرورگر
        var items = rows.Select(x => new
        {
            id = x.Id,
            chequeNumber = x.ChequeNumber,
            direction = (int)x.Direction,
            directionName = x.Direction == ChequeDirection.Received ? "دریافتنی" : "پرداختنی",
            contactName = x.ContactName,
            drawerName = x.DrawerName,
            bankName = x.BankName,
            amount = x.Amount,
            dueDate = x.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            dueDateShamsi = PersianDate.Format(x.DueDate),
            status = (int)x.Status,
            statusName = GetStatusName(x.Status),
            nextFollowUpDate = x.NextFollowUpDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            nextFollowUpShamsi = PersianDate.Format(x.NextFollowUpDate)
        });

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // ============================================
    // Create
    // ============================================

    [Authorize(Policy = "Cheque.Create")]
    public async Task<IActionResult> Create(ChequeDirection direction = ChequeDirection.Received)
    {
        var today = _timeProvider.Today();

        var model = new ChequeFormViewModel
        {
            Direction = direction,
            IssueDate = PersianDate.Format(today),
            DueDate = PersianDate.Format(today.AddMonths(1)),
            ContactOptions = await GetContactOptionsAsync(direction),
            BankAccountOptions = await GetBankAccountOptionsAsync(),
            InvoiceOptions = await GetInvoiceOptionsAsync(direction)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Create")]
    public async Task<IActionResult> Create(ChequeFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var request = ParseRequest(model);

        if (!ModelState.IsValid || request == null)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        try
        {
            var cheque = await _chequeService.CreateAsync(request, tenantId.Value);

            TempData["Success"] = $"چک شماره {cheque.ChequeNumber} با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index), new { direction = (int)cheque.Direction });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await ReloadOptionsAsync(model);
            return View(model);
        }
    }

    // ============================================
    // Edit
    // ============================================

    [Authorize(Policy = "Cheque.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Cheques
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (!ChequeRules.IsEditable(entity.Status))
        {
            TempData["Error"] = "چک در وضعیت فعلی قابل ویرایش نیست.";
            return RedirectToAction(nameof(Index), new { direction = (int)entity.Direction });
        }

        var model = new ChequeFormViewModel
        {
            Id = entity.Id,
            ChequeNumber = entity.ChequeNumber,
            Direction = entity.Direction,
            Amount = entity.Amount,
            IssueDate = PersianDate.Format(entity.IssueDate),
            DueDate = PersianDate.Format(entity.DueDate),
            ContactId = entity.ContactId,
            BankAccountId = entity.BankAccountId,
            BankName = entity.BankName,
            AccountNumber = entity.AccountNumber,
            BranchCode = entity.BranchCode,
            BranchName = entity.BranchName,
            DrawerName = entity.DrawerName,
            DrawerNationalId = entity.DrawerNationalId,
            Description = entity.Description,
            InvoiceId = entity.InvoiceId,
            ImagePath = entity.ImagePath,
            ContactOptions = await GetContactOptionsAsync(entity.Direction),
            BankAccountOptions = await GetBankAccountOptionsAsync(),
            InvoiceOptions = await GetInvoiceOptionsAsync(entity.Direction)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public async Task<IActionResult> Edit(int id, ChequeFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        var request = ParseRequest(model);

        if (!ModelState.IsValid || request == null)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        try
        {
            // سرویس: وضعیت قابل ویرایش، ثابت بودن جهت، اعتبارسنجی و بازسازی سند ثبت
            await _chequeService.UpdateAsync(id, request, tenantId.Value);

            TempData["Success"] = "چک با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Index), new { direction = (int)model.Direction });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await ReloadOptionsAsync(model);
            return View(model);
        }
    }

    // ============================================
    // Details
    // ============================================

    [Authorize(Policy = "Cheque.View")]
    public async Task<IActionResult> Details(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Cheques
            .Include(c => c.Contact)
            .Include(c => c.BankAccount)
            .Include(c => c.Invoice)
            .Include(c => c.Transaction)
            .Include(c => c.Histories)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        return View(entity);
    }

    // ============================================
    // Delete
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Delete")]
    public Task<IActionResult> Delete(int id)
        => RunAjaxAsync(tenantId => _chequeService.DeleteAsync(id, tenantId), "چک با موفقیت حذف شد.");

    // ============================================
    // گردش کار
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsDeposited(int id, string? description)
        => RunAjaxAsync(tenantId => _chequeService.MarkAsDepositedAsync(id, tenantId, description));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsCleared(int id, string? clearedDate, string? bankTrackingNumber)
    {
        if (!TryParseOptionalDate(clearedDate, out var date))
            return InvalidDateResult();

        return RunAjaxAsync(tenantId => _chequeService.MarkAsClearedAsync(id, tenantId, date, bankTrackingNumber));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsBounced(int id, string? bounceReason, string? bouncedDate)
    {
        if (!TryParseOptionalDate(bouncedDate, out var date))
            return InvalidDateResult();

        return RunAjaxAsync(tenantId => _chequeService.MarkAsBouncedAsync(id, tenantId, bounceReason, date));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsCashed(int id, string? cashedDate, string? description)
    {
        if (!TryParseOptionalDate(cashedDate, out var date))
            return InvalidDateResult();

        return RunAjaxAsync(tenantId => _chequeService.MarkAsCashedAsync(id, tenantId, date, description));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsRePresented(int id, string newDueDate, string? description)
    {
        // سررسید جدید الزامی است؛ تاریخ نامعتبر دیگر به «تاریخ خالی» تبدیل نمی‌شود
        if (!PersianDate.TryParse(newDueDate, out var dueDate))
            return InvalidDateResult();

        return RunAjaxAsync(tenantId => _chequeService.MarkAsRePresentedAsync(id, tenantId, dueDate, description));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsReturned(int id, string? description)
        => RunAjaxAsync(tenantId => _chequeService.MarkAsReturnedAsync(id, tenantId, description));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsReferred(int id, string? description)
        => RunAjaxAsync(tenantId => _chequeService.MarkAsReferredAsync(id, tenantId, description));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> MarkAsCancelled(int id, string? reason)
        => RunAjaxAsync(tenantId => _chequeService.MarkAsCancelledAsync(id, tenantId, reason));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> Unclear(int id, string? reason)
        => RunAjaxAsync(tenantId => _chequeService.UnclearAsync(id, tenantId, reason));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Cheque.Edit")]
    public Task<IActionResult> Uncash(int id, string? reason)
        => RunAjaxAsync(tenantId => _chequeService.UncashAsync(id, tenantId, reason));

    // ============================================
    // Helpers
    // ============================================

    /// <summary>
    /// خطای کسب‌وکار (InvalidOperationException) پیام فارسی به کاربر می‌دهد.
    /// هر خطای دیگر لاگ می‌شود و پیام عمومی برمی‌گردد (جزئیات داخلی نشت نمی‌کند).
    /// </summary>
    private async Task<IActionResult> RunAjaxAsync(
        Func<int, Task> action, string successMessage = "عملیات با موفقیت انجام شد.")
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Json(new { success = false, message = NoTenantMessage });

        try
        {
            await action(tenantId.Value);
            return Json(new { success = true, message = successMessage });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cheque operation failed");
            return Json(new { success = false, message = UnexpectedErrorMessage });
        }
    }

    private Task<IActionResult> InvalidDateResult()
        => Task.FromResult<IActionResult>(Json(new { success = false, message = InvalidDateMessage }));

    /// <summary>خالی = «تاریخ نداده» (مجاز، سرویس امروز را می‌گذارد)؛ پر ولی نامعتبر = خطا.</summary>
    private static bool TryParseOptionalDate(string? text, out DateOnly? date)
    {
        date = null;

        if (string.IsNullOrWhiteSpace(text))
            return true;

        if (!PersianDate.TryParse(text, out var parsed))
            return false;

        date = parsed;
        return true;
    }

    /// <summary>
    /// فرم → درخواست. تاریخ نامعتبر خطای فیلد می‌شود (قبلاً بی‌صدا 0001/01/01 ذخیره می‌شد)
    /// و در آن حالت null برمی‌گردد.
    /// </summary>
    private ChequeRequest? ParseRequest(ChequeFormViewModel model)
    {
        var issueOk = PersianDate.TryParse(model.IssueDate, out var issueDate);
        var dueOk = PersianDate.TryParse(model.DueDate, out var dueDate);

        if (!issueOk)
            ModelState.AddModelError(nameof(model.IssueDate), InvalidDateMessage);

        if (!dueOk)
            ModelState.AddModelError(nameof(model.DueDate), InvalidDateMessage);

        if (!issueOk || !dueOk)
            return null;

        return new ChequeRequest
        {
            ChequeNumber = PersianDate.NormalizeDigits(model.ChequeNumber).Trim(),
            Direction = model.Direction,
            Amount = model.Amount,
            IssueDate = issueDate,
            DueDate = dueDate,
            ContactId = model.ContactId,
            BankAccountId = model.BankAccountId,
            BankName = model.BankName,
            AccountNumber = model.AccountNumber,
            BranchCode = model.BranchCode,
            BranchName = model.BranchName,
            DrawerName = model.DrawerName,
            DrawerNationalId = model.DrawerNationalId,
            Description = model.Description,
            InvoiceId = model.InvoiceId,
            ImagePath = model.ImagePath
        };
    }

    private async Task ReloadOptionsAsync(ChequeFormViewModel model)
    {
        model.ContactOptions = await GetContactOptionsAsync(model.Direction);
        model.BankAccountOptions = await GetBankAccountOptionsAsync();
        model.InvoiceOptions = await GetInvoiceOptionsAsync(model.Direction);
    }

    private async Task<List<SelectListItem>> GetContactOptionsAsync(ChequeDirection direction)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        var query = _context.Contacts.Where(c => c.TenantId == tenantId.Value && c.IsActive);

        if (direction == ChequeDirection.Received)
            query = query.Where(c => c.ContactType == ContactType.Customer || c.ContactType == ContactType.Both);
        else
            query = query.Where(c => c.ContactType == ContactType.Supplier || c.ContactType == ContactType.Both);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetBankAccountOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Accounts
            .Where(a => a.TenantId == tenantId.Value && a.IsActive && a.AccountType == AccountType.Bank)
            .OrderBy(a => a.Name)
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = a.Name + (a.BankName != null ? $" - {a.BankName}" : "")
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetInvoiceOptionsAsync(ChequeDirection direction)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        var invoiceType = direction == ChequeDirection.Received ? InvoiceType.Sale : InvoiceType.Purchase;

        return await _context.Invoices
            .Where(i => i.TenantId == tenantId.Value && i.InvoiceType == invoiceType && i.Status == InvoiceStatus.Posted)
            .OrderByDescending(i => i.Date)
            .Select(i => new SelectListItem
            {
                Value = i.Id.ToString(),
                Text = $"فاکتور شماره {i.Number}"
            })
            .ToListAsync();
    }

    private static string GetStatusName(ChequeStatus status)
    {
        return status switch
        {
            ChequeStatus.InProgress => "در جریان",
            ChequeStatus.Cleared => "پاس شده",
            ChequeStatus.Bounced => "برگشت خورده",
            ChequeStatus.Returned => "عودت داده شده",
            ChequeStatus.RePresented => "مجدداً ارائه",
            ChequeStatus.Cancelled => "ابطال شده",
            ChequeStatus.Referred => "ارجاع به صاحب چک",
            ChequeStatus.Cashed => "نقد شده",
            _ => "نامشخص"
        };
    }
}
