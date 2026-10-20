using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Accounting.ViewModels.FiscalYear;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class FiscalYearsController : Controller
{
    private const string InvalidDateMessage = "تاریخ نامعتبر است. نمونه‌ی درست: 1405/01/01";
    private const string UnexpectedErrorMessage = "خطای غیرمنتظره رخ داد. لطفاً دوباره تلاش کنید.";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IFiscalYearService _fiscalYearService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FiscalYearsController> _logger;

    public FiscalYearsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IFiscalYearService fiscalYearService,
        TimeProvider timeProvider,
        ILogger<FiscalYearsController> logger)
    {
        _context = context;
        _currentTenant = currentTenant;
        _fiscalYearService = fiscalYearService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ============================================
    // Index
    // ============================================

    [Authorize(Policy = "FiscalYear.View")]
    public async Task<IActionResult> Index()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var today = _timeProvider.Today();

        var rows = await _context.FiscalYears
            .Where(f => f.TenantId == tenantId.Value)
            .OrderByDescending(f => f.StartDate)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.StartDate,
                f.EndDate,
                f.Status,
                f.ClosedAt,
                TransactionCount = _context.Transactions.Count(t => t.FiscalYearId == f.Id)
            })
            .ToListAsync();

        var model = rows.Select(r => new FiscalYearListItemViewModel
        {
            Id = r.Id,
            Name = r.Name,
            StartDate = PersianDate.Format(r.StartDate),
            EndDate = PersianDate.Format(r.EndDate),
            IsClosed = r.Status == FiscalYearStatus.Closed,
            IsCurrent = r.StartDate <= today && r.EndDate >= today,
            TransactionCount = r.TransactionCount,
            ClosedAt = r.ClosedAt.HasValue
                ? PersianDate.Format(_timeProvider.ToLocalDate(r.ClosedAt.Value))
                : null
        }).ToList();

        return View(model);
    }

    // ============================================
    // Create
    // ============================================

    [Authorize(Policy = "FiscalYear.Create")]
    public async Task<IActionResult> Create()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        // پیشنهاد: روز بعد از آخرین سال تعریف‌شده، یا ابتدای سال شمسی جاری
        var last = await _context.FiscalYears
            .Where(f => f.TenantId == tenantId.Value)
            .OrderByDescending(f => f.StartDate)
            .FirstOrDefaultAsync();

        var start = last != null
            ? last.EndDate.AddDays(1)
            : PersianDate.StartOfYear(_timeProvider.Today());

        var end = PersianDate.AddMonths(start, 12).AddDays(-1);

        return View(new FiscalYearFormViewModel
        {
            StartDate = PersianDate.Format(start),
            EndDate = PersianDate.Format(end)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "FiscalYear.Create")]
    public async Task<IActionResult> Create(FiscalYearFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var request = ParseRequest(model);
        if (!ModelState.IsValid || request == null)
            return View(model);

        try
        {
            var year = await _fiscalYearService.CreateAsync(request, tenantId.Value);

            TempData["Success"] = $"«{year.Name}» ثبت شد.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    // ============================================
    // Edit
    // ============================================

    [Authorize(Policy = "FiscalYear.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.FiscalYears
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (entity.Status == FiscalYearStatus.Closed)
        {
            TempData["Error"] = "سال مالی بسته قابل ویرایش نیست. ابتدا آن را باز کنید.";
            return RedirectToAction(nameof(Index));
        }

        return View(new FiscalYearFormViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            StartDate = PersianDate.Format(entity.StartDate),
            EndDate = PersianDate.Format(entity.EndDate)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "FiscalYear.Edit")]
    public async Task<IActionResult> Edit(int id, FiscalYearFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        var request = ParseRequest(model);
        if (!ModelState.IsValid || request == null)
            return View(model);

        try
        {
            await _fiscalYearService.UpdateAsync(id, request, tenantId.Value);

            TempData["Success"] = "سال مالی ویرایش شد.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    // ============================================
    // بستن / باز کردن / حذف
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "FiscalYear.Close")]
    public Task<IActionResult> Close(int id)
        => RunAsync(tenantId => _fiscalYearService.CloseAsync(id, tenantId), "سال مالی بسته شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "FiscalYear.Close")]
    public Task<IActionResult> Reopen(int id)
        => RunAsync(tenantId => _fiscalYearService.ReopenAsync(id, tenantId), "سال مالی دوباره باز شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "FiscalYear.Delete")]
    public Task<IActionResult> Delete(int id)
        => RunAsync(tenantId => _fiscalYearService.DeleteAsync(id, tenantId), "سال مالی حذف شد.");

    // ============================================
    // Helpers
    // ============================================

    /// <summary>
    /// عملیات با فرم POST ساده (بدون AJAX): نتیجه با TempData در لیست نمایش داده می‌شود.
    /// خطای کسب‌وکار پیام فارسی می‌دهد؛ هر خطای دیگر لاگ می‌شود و پیام عمومی نمایش داده می‌شود.
    /// </summary>
    private async Task<IActionResult> RunAsync(Func<int, Task> action, string successMessage)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        try
        {
            await action(tenantId.Value);
            TempData["Success"] = successMessage;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fiscal year operation failed");
            TempData["Error"] = UnexpectedErrorMessage;
        }

        return RedirectToAction(nameof(Index));
    }

    private FiscalYearRequest? ParseRequest(FiscalYearFormViewModel model)
    {
        var startOk = PersianDate.TryParse(model.StartDate, out var start);
        var endOk = PersianDate.TryParse(model.EndDate, out var end);

        if (!startOk)
            ModelState.AddModelError(nameof(model.StartDate), InvalidDateMessage);

        if (!endOk)
            ModelState.AddModelError(nameof(model.EndDate), InvalidDateMessage);

        if (!startOk || !endOk)
            return null;

        return new FiscalYearRequest
        {
            Name = model.Name,
            StartDate = start,
            EndDate = end
        };
    }
}
