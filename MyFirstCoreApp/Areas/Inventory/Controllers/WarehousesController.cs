using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Areas.Inventory.ViewModels.Warehouse;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public class WarehousesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public WarehousesController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // GET: /Inventory/Warehouses
    [Authorize(Policy = "Warehouse.View")]
    public IActionResult Index() => View();

    // POST: /Inventory/Warehouses/GetData
    [HttpPost]
    [Authorize(Policy = "Warehouse.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Json(new { success = false, message = "شرکت جاری مشخص نیست." });

        var query = _context.Warehouses
            .Where(x => x.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.Code.Contains(search) ||
                x.Name.Contains(search) ||
                (x.Address != null && x.Address.Contains(search)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "code" => request.SortDir == "asc"
                ? query.OrderBy(x => x.Code)
                : query.OrderByDescending(x => x.Code),
            "name" => request.SortDir == "asc"
                ? query.OrderBy(x => x.Name)
                : query.OrderByDescending(x => x.Name),
            "isActive" => request.SortDir == "asc"
                ? query.OrderBy(x => x.IsActive)
                : query.OrderByDescending(x => x.IsActive),
            _ => query.OrderBy(x => x.Id)
        };

        var take = Math.Max(0, request.EndRow - request.StartRow);
        var items = await query
            .Skip(Math.Max(0, request.StartRow))
            .Take(take)
            .Select(x => new
            {
                id = x.Id,
                code = x.Code,
                name = x.Name,
                address = x.Address,
                isActive = x.IsActive,
                isDefault = x.IsDefault
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // GET: /Inventory/Warehouses/Create
    [Authorize(Policy = "Warehouse.Create")]
    public IActionResult Create() => View(new WarehouseFormViewModel());

    // POST: /Inventory/Warehouses/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Warehouse.Create")]
    public async Task<IActionResult> Create(WarehouseFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        model.Code = model.Code.Trim();
        model.Name = model.Name.Trim();

        if (model.IsDefault && !model.IsActive)
            ModelState.AddModelError(nameof(model.IsDefault), "انبار پیش‌فرض باید فعال باشد.");

        var duplicateCode = await _context.Warehouses.AnyAsync(x =>
            x.TenantId == tenantId.Value && x.Code == model.Code);
        if (duplicateCode)
            ModelState.AddModelError(nameof(model.Code), "این کد انبار قبلاً ثبت شده است.");

        if (!ModelState.IsValid) return View(model);

        var entity = new Warehouse
        {
            TenantId = tenantId.Value,
            Code = model.Code,
            Name = model.Name,
            Address = model.Address?.Trim(),
            Description = model.Description?.Trim(),
            IsActive = model.IsActive,
            IsDefault = false
        };

        _context.Warehouses.Add(entity);
        await _context.SaveChangesAsync();

        if (model.IsDefault)
        {
            try
            {
                await new WarehouseService(_context)
                    .SetDefaultWarehouseAsync(entity.Id, tenantId.Value);
            }
            catch
            {
                // Avoid reporting a successful default selection if it failed.
                entity.IsDefault = false;
                await _context.SaveChangesAsync();
                throw;
            }
        }

        TempData["Success"] = "انبار با موفقیت ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Inventory/Warehouses/Edit/5
    [Authorize(Policy = "Warehouse.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Warehouses
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity is null) return NotFound();

        return View(new WarehouseFormViewModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Address = entity.Address,
            Description = entity.Description,
            IsActive = entity.IsActive,
            IsDefault = entity.IsDefault
        });
    }

    // POST: /Inventory/Warehouses/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Warehouse.Edit")]
    public async Task<IActionResult> Edit(int id, WarehouseFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();
        if (id != model.Id) return BadRequest();

        model.Code = model.Code.Trim();
        model.Name = model.Name.Trim();

        if (model.IsDefault && !model.IsActive)
            ModelState.AddModelError(nameof(model.IsDefault), "انبار پیش‌فرض باید فعال باشد.");

        var duplicateCode = await _context.Warehouses.AnyAsync(x =>
            x.TenantId == tenantId.Value &&
            x.Id != id &&
            x.Code == model.Code);
        if (duplicateCode)
            ModelState.AddModelError(nameof(model.Code), "این کد انبار قبلاً ثبت شده است.");

        var entity = await _context.Warehouses
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        entity.Code = model.Code;
        entity.Name = model.Name;
        entity.Address = model.Address?.Trim();
        entity.Description = model.Description?.Trim();
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        // اگر انبار غیرفعال شود، دیگر نمی‌تواند پیش‌فرض بماند.
        if (!entity.IsActive)
            entity.IsDefault = false;
        else if (!model.IsDefault)
            entity.IsDefault = false;

        await _context.SaveChangesAsync();

        if (model.IsDefault)
        {
            await new WarehouseService(_context)
                .SetDefaultWarehouseAsync(entity.Id, tenantId.Value);
        }

        TempData["Success"] = "انبار با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Inventory/Warehouses/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Warehouse.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Json(new { success = false, message = "شرکت جاری مشخص نیست." });

        var entity = await _context.Warehouses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity is null)
            return Json(new { success = false, message = "انبار پیدا نشد." });

        if (entity.IsDeleted)
            return Json(new { success = false, message = "این انبار قبلاً حذف شده است." });

        var hasMovements = await _context.StockMovements
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId.Value &&
                           !x.IsDeleted &&
                           (x.SourceWarehouseId == id || x.DestinationWarehouseId == id));

        if (hasMovements)
            return Json(new
            {
                success = false,
                message = "این انبار در گردش‌های انبار استفاده شده و قابل حذف نیست."
            });

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.IsDefault = false;
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "انبار با موفقیت حذف شد." });
    }
}