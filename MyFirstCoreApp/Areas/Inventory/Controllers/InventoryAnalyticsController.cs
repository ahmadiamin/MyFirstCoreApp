using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public sealed class InventoryAnalyticsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _tenant;
    private readonly IInventoryAnalyticsService _analytics;
    private readonly TimeProvider _timeProvider;

    public InventoryAnalyticsController(ApplicationDbContext context, ICurrentTenant tenant,
        IInventoryAnalyticsService analytics, TimeProvider timeProvider)
    {
        _context = context;
        _tenant = tenant;
        _analytics = analytics;
        _timeProvider = timeProvider;
    }

    [Authorize(Policy = "MinimumStock.View")]
    public async Task<IActionResult> MinimumStock()
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var rows = await _analytics.GetBelowMinimumStockAsync(_tenant.TenantId.Value);
        return View(rows);
    }

    [Authorize(Policy = "InventoryValuation.View")]
    public async Task<IActionResult> Valuation(string? asOf, int? warehouseId, int? productId, bool search = false)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var tenantId = _tenant.TenantId.Value;
        var warehouses = await _context.Warehouses.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        var products = await _context.Products.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TrackInventory && x.IsActive).OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync();
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var dateText = string.IsNullOrWhiteSpace(asOf) ? PersianDate.Format(today) : asOf;
        var model = new InventoryValuationViewModel
        {
            AsOf = dateText,
            WarehouseId = warehouseId,
            ProductId = productId,
            HasSearched = search,
            Warehouses = warehouses,
            Products = products
        };
        if (search)
        {
            if (!PersianDate.TryParse(dateText, out var selectedDate))
                ModelState.AddModelError(nameof(model.AsOf), "تاریخ واردشده معتبر نیست.");
            else try
            {
                model.Rows = await _analytics.GetInventoryValuationAsync(tenantId, selectedDate, warehouseId, productId);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        return View(model);
    }
}

public sealed class InventoryValuationViewModel
{
    public string AsOf { get; set; } = string.Empty;
    public int? WarehouseId { get; set; }
    public int? ProductId { get; set; }
    public bool HasSearched { get; set; }
    public List<SelectListItem> Warehouses { get; set; } = [];
    public List<SelectListItem> Products { get; set; } = [];
    public IReadOnlyList<Application.Inventory.InventoryValuationDto> Rows { get; set; } = [];
}
