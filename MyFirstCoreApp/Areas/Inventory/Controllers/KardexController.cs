using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Inventory.ViewModels.Kardex;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public class KardexController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStockMovementService _stockMovementService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KardexController> _logger;

    public KardexController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IStockMovementService stockMovementService,
        TimeProvider timeProvider,
        ILogger<KardexController> logger)
    {
        _context = context;
        _currentTenant = currentTenant;
        _stockMovementService = stockMovementService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [Authorize(Policy = "Kardex.View")]
    public async Task<IActionResult> Index(
        int? productId = null,
        int? warehouseId = null,
        string? fromDate = null,
        string? toDate = null)
    {
        var tenantId = _currentTenant.TenantId;

        if (!tenantId.HasValue)
            return Forbid();

        var today = _timeProvider.Today();

        var model = new KardexReportViewModel
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            FromDate = string.IsNullOrWhiteSpace(fromDate)
                ? PersianDate.Format(PersianDate.StartOfMonth(today))
                : fromDate,
            ToDate = string.IsNullOrWhiteSpace(toDate)
                ? PersianDate.Format(today)
                : toDate,
            ProductOptions = await GetProductOptionsAsync(tenantId.Value),
            WarehouseOptions = await GetWarehouseOptionsAsync(tenantId.Value)
        };

        if (!productId.HasValue || !warehouseId.HasValue)
            return View(model);

        if (!PersianDate.TryParse(model.FromDate, out var from) ||
            !PersianDate.TryParse(model.ToDate, out var to))
        {
            TempData["Error"] =
                "تاریخ شروع یا پایان نامعتبر است. نمونه‌ی درست: 1405/01/10";

            return View(model);
        }

        if (from > to)
        {
            TempData["Error"] = "تاریخ شروع نباید بعد از تاریخ پایان باشد.";
            return View(model);
        }

        try
        {
            // هر دو انتخاب باید متعلق به شرکت جاری و فعال باشند.
            var product = await _context.Products
                .AsNoTracking()
                .Where(x =>
                    x.Id == productId.Value &&
                    x.TenantId == tenantId.Value &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Code,
                    UnitName = x.Unit.Name
                })
                .FirstOrDefaultAsync();

            if (product is null)
            {
                TempData["Error"] =
                    "کالای انتخاب‌شده یافت نشد یا غیرفعال است.";

                model.ProductId = null;
                return View(model);
            }

            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .Where(x =>
                    x.Id == warehouseId.Value &&
                    x.TenantId == tenantId.Value &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .FirstOrDefaultAsync();

            if (warehouse is null)
            {
                TempData["Error"] =
                    "انبار انتخاب‌شده یافت نشد یا غیرفعال است.";

                model.WarehouseId = null;
                return View(model);
            }

            model.ProductName = product.Name;
            model.ProductCode = product.Code;
            model.UnitName = product.UnitName;
            model.WarehouseName = warehouse.Name;

            model.Rows = await _stockMovementService.GetKardexAsync(
                product.Id,
                warehouse.Id,
                from,
                to,
                tenantId.Value);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kardex report failed");

            TempData["Error"] =
                "خطای غیرمنتظره در تهیه کاردکس رخ داد.";
        }

        return View(model);
    }

    private async Task<List<SelectListItem>> GetProductOptionsAsync(
        int tenantId)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Code == null
                    ? x.Name
                    : x.Code + " - " + x.Name
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetWarehouseOptionsAsync(
        int tenantId)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.IsDefault
                    ? x.Name + " (پیش‌فرض)"
                    : x.Name
            })
            .ToListAsync();
    }
}