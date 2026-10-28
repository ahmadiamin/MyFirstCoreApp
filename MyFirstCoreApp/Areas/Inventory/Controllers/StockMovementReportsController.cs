using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Areas.Inventory.ViewModels.StockMovementReports;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public sealed class StockMovementReportsController : Controller
{
    private const int MaximumRows = 500;
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly TimeProvider _timeProvider;

    public StockMovementReportsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentTenant = currentTenant;
        _timeProvider = timeProvider;
    }

    [Authorize(Policy = "StockMovementReport.View")]
    public async Task<IActionResult> Index(
        int? warehouseId = null,
        int? productId = null,
        int? movementType = null,
        int? status = null,
        string? fromDate = null,
        string? toDate = null,
        bool search = false)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var tenantId = _currentTenant.TenantId.Value;
        var today = _timeProvider.Today();

        var model = new StockMovementReportViewModel
        {
            WarehouseId = warehouseId,
            ProductId = productId,
            MovementType = movementType,
            Status = status,
            FromDate = string.IsNullOrWhiteSpace(fromDate)
                ? PersianDate.Format(PersianDate.StartOfMonth(today))
                : fromDate,
            ToDate = string.IsNullOrWhiteSpace(toDate)
                ? PersianDate.Format(today)
                : toDate,
            HasSearched = search,
            Warehouses = await GetWarehouseOptionsAsync(tenantId),
            Products = await GetProductOptionsAsync(tenantId)
        };

        if (!search) return View(model);

        if (!PersianDate.TryParse(model.FromDate, out var from) ||
            !PersianDate.TryParse(model.ToDate, out var to))
        {
            ModelState.AddModelError(string.Empty, "تاریخ شروع یا پایان نامعتبر است.");
            return View(model);
        }
        if (from > to)
        {
            ModelState.AddModelError(string.Empty, "تاریخ شروع نباید بعد از تاریخ پایان باشد.");
            return View(model);
        }
        if (movementType.HasValue && !Enum.IsDefined(typeof(StockMovementType), movementType.Value))
        {
            ModelState.AddModelError(string.Empty, "نوع گردش انبار معتبر نیست.");
            return View(model);
        }
        if (status.HasValue && !Enum.IsDefined(typeof(StockMovementStatus), status.Value))
        {
            ModelState.AddModelError(string.Empty, "وضعیت سند معتبر نیست.");
            return View(model);
        }

        var query = _context.StockMovements
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Date >= from && x.Date <= to);

        if (warehouseId.HasValue)
        {
            var warehouseExists = await _context.Warehouses.AnyAsync(x =>
                x.Id == warehouseId.Value && x.TenantId == tenantId && x.IsActive);
            if (!warehouseExists)
            {
                ModelState.AddModelError(string.Empty, "انبار انتخاب‌شده معتبر نیست.");
                model.WarehouseId = null;
                return View(model);
            }
            query = query.Where(x => x.SourceWarehouseId == warehouseId || x.DestinationWarehouseId == warehouseId);
        }

        if (productId.HasValue)
        {
            var productExists = await _context.Products.AnyAsync(x =>
                x.Id == productId.Value && x.TenantId == tenantId && x.IsActive && x.TrackInventory);
            if (!productExists)
            {
                ModelState.AddModelError(string.Empty, "کالای انتخاب‌شده معتبر نیست.");
                model.ProductId = null;
                return View(model);
            }
            query = query.Where(x => x.Items.Any(i => i.ProductId == productId.Value));
        }

        if (movementType.HasValue)
            query = query.Where(x => (int)x.MovementType == movementType.Value);
        if (status.HasValue)
            query = query.Where(x => (int)x.Status == status.Value);

        var movements = await query
            .Include(x => x.SourceWarehouse)
            .Include(x => x.DestinationWarehouse)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .ThenInclude(x => x.Unit)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Take(MaximumRows + 1)
            .ToListAsync();

        model.Rows = movements
            .Take(MaximumRows)
            .Select(x => new StockMovementReportRowViewModel
            {
                Id = x.Id,
                Number = x.Number,
                Date = PersianDate.Format(x.Date),
                MovementType = x.MovementType switch
                {
                    StockMovementType.Opening => "افتتاحیه",
                    StockMovementType.Receipt => "رسید",
                    StockMovementType.Issue => "حواله خروج",
                    StockMovementType.Transfer => "انتقال",
                    _ => "نامشخص"
                },
                Status = x.Status switch
                {
                    StockMovementStatus.Draft => "پیش‌نویس",
                    StockMovementStatus.Posted => "قطعی",
                    StockMovementStatus.Cancelled => "لغوشده",
                    _ => "نامشخص"
                },
                SourceWarehouse = x.SourceWarehouse?.Name ?? "—",
                DestinationWarehouse = x.DestinationWarehouse?.Name ?? "—",
                ItemsSummary = string.Join("، ", x.Items.Select(i =>
                    $"{i.Product?.Name ?? "کالای نامشخص"} × {i.Quantity:N3} {i.Product?.Unit?.Symbol}")),
                Description = x.Description
            })
            .ToList();
        ViewData["HasMoreRows"] = movements.Count > MaximumRows;
        return View(model);
    }

    private async Task<List<SelectListItem>> GetWarehouseOptionsAsync(int tenantId)
    {
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.IsDefault })
            .ToListAsync();
        return warehouses.Select(x => new SelectListItem(
            x.IsDefault ? x.Name + " (پیش‌فرض)" : x.Name,
            x.Id.ToString())).ToList();
    }

    private async Task<List<SelectListItem>> GetProductOptionsAsync(int tenantId)
    {
        var products = await _context.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.TrackInventory)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToListAsync();
        return products.Select(x => new SelectListItem(
            x.Code == null ? x.Name : x.Code + " - " + x.Name,
            x.Id.ToString())).ToList();
    }
}
