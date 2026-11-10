using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Inventory.ViewModels.StockBalances;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public sealed class StockAdjustmentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStockMovementService _stockMovementService;

    public StockAdjustmentsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IStockMovementService stockMovementService)
    {
        _context = context;
        _currentTenant = currentTenant;
        _stockMovementService = stockMovementService;
    }

    [Authorize(Policy = "StockMovement.Create")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id, int? warehouseId)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var tenantId = _currentTenant.TenantId.Value;

        var product = await _context.Products
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id, x.Name, x.Code, UnitName = x.Unit.Name,
                x.TrackInventory, x.IsActive
            })
            .FirstOrDefaultAsync();

        if (product is null) return NotFound();

        if (!product.TrackInventory || !product.IsActive)
        {
            TempData["Error"] = "اصلاح موجودی فقط برای کالای فعال با کنترل موجودی امکان‌پذیر است.";
            return RedirectToAction("Index", "Products");
        }

        var warehouses = await GetWarehousesAsync(tenantId);
        if (warehouses.Count == 0)
        {
            TempData["Error"] = "ابتدا یک انبار فعال تعریف کنید.";
            return RedirectToAction("Index", "Products");
        }

        var selectedWarehouseId = warehouseId.HasValue &&
                                  warehouses.Any(x => x.Value == warehouseId.Value.ToString())
            ? warehouseId.Value
            : int.Parse(warehouses[0].Value!);

        var currentQuantity = await _stockMovementService.GetBalanceAsync(
            id, selectedWarehouseId, tenantId);

        return View(new StockAdjustmentViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            ProductCode = product.Code,
            UnitName = product.UnitName,
            WarehouseId = selectedWarehouseId,
            CurrentQuantity = currentQuantity,
            TargetQuantity = currentQuantity,
            Warehouses = warehouses
        });
    }

    [Authorize(Policy = "StockMovement.Create")]
    [Authorize(Policy = "StockMovement.Post")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StockAdjustmentViewModel model)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var tenantId = _currentTenant.TenantId.Value;

        var product = await _context.Products
            .AsNoTracking()
            .Where(x => x.Id == model.ProductId && x.TenantId == tenantId)
            .Select(x => new { x.Id, x.Name, x.Code, UnitName = x.Unit.Name })
            .FirstOrDefaultAsync();

        if (product is null) return NotFound();

        model.ProductName = product.Name;
        model.ProductCode = product.Code;
        model.UnitName = product.UnitName;

        if (!ModelState.IsValid)
        {
            await PopulateFormAsync(model, tenantId);
            return View(model);
        }

        try
        {
            var movementId = await _stockMovementService.AdjustBalanceAsync(
                model.ProductId,
                model.WarehouseId,
                model.TargetQuantity,
                model.UnitCost,
                DateOnly.FromDateTime(DateTime.Today),
                tenantId);

            TempData["Success"] = movementId == 0
                ? "موجودی واردشده با موجودی فعلی برابر است؛ تغییری لازم نبود."
                : "موجودی کالا با موفقیت اصلاح و گردش انبار ثبت شد.";

            return RedirectToAction(nameof(Edit), new
            {
                id = model.ProductId,
                warehouseId = model.WarehouseId
            });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateFormAsync(model, tenantId);
            return View(model);
        }
    }

    private async Task PopulateFormAsync(StockAdjustmentViewModel model, int tenantId)
    {
        model.Warehouses = await GetWarehousesAsync(tenantId);
        if (model.WarehouseId > 0)
            model.CurrentQuantity = await _stockMovementService.GetBalanceAsync(
                model.ProductId, model.WarehouseId, tenantId);
    }

    private async Task<List<SelectListItem>> GetWarehousesAsync(int tenantId)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem { Value = x.Id.ToString(), Text = x.Name })
            .ToListAsync();
    }
}
