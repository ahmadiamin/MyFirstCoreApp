using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Inventory.ViewModels.StockBalances;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public sealed class DormantStockController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStockBalanceService _stockBalanceService;
    private readonly TimeProvider _timeProvider;

    public DormantStockController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IStockBalanceService stockBalanceService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentTenant = currentTenant;
        _stockBalanceService = stockBalanceService;
        _timeProvider = timeProvider;
    }

    [Authorize(Policy = "DormantStock.View")]
    public async Task<IActionResult> Index(
        int? warehouseId = null,
        int? productId = null,
        string? fromDate = null,
        string? toDate = null,
        bool search = false)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var tenantId = _currentTenant.TenantId.Value;
        var today = _timeProvider.Today();
        var model = new DormantStockReportViewModel
        {
            WarehouseId = warehouseId,
            ProductId = productId,
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

        try
        {
            var rows = await _stockBalanceService.GetDormantBalancesAsync(
                tenantId, from, to, warehouseId, productId);
            model.Rows = rows.Select(x => new DormantStockReportRowViewModel
            {
                ProductId = x.ProductId,
                WarehouseId = x.WarehouseId,
                ProductName = x.ProductName,
                ProductCode = x.ProductCode,
                UnitName = x.UnitName,
                WarehouseName = x.WarehouseName,
                Quantity = x.Quantity,
                LastMovementDate = PersianDate.Format(x.LastMovementDate),
                DaysWithoutMovement = to.DayNumber - x.LastMovementDate.DayNumber
            }).ToList();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        return View(model);
    }

    private async Task<List<SelectListItem>> GetWarehouseOptionsAsync(int tenantId)
    {
        var items = await _context.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.IsDefault })
            .ToListAsync();
        return items.Select(x => new SelectListItem(
            x.IsDefault ? x.Name + " (پیش‌فرض)" : x.Name, x.Id.ToString())).ToList();
    }

    private async Task<List<SelectListItem>> GetProductOptionsAsync(int tenantId)
    {
        var items = await _context.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.TrackInventory)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Code })
            .ToListAsync();
        return items.Select(x => new SelectListItem(
            x.Code == null ? x.Name : x.Code + " - " + x.Name, x.Id.ToString())).ToList();
    }
}
