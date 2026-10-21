using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Areas.Inventory.ViewModels.Product;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public ProductsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // GET: /Inventory/Products
    [Authorize(Policy = "Product.View")]
    public IActionResult Index() => View();

    // POST: /Inventory/Products/GetData
    [HttpPost]
    [Authorize(Policy = "Product.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Products
            .Where(x => x.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x =>
                (x.Name != null && x.Name.Contains(s)) ||
                (x.Code != null && x.Code.Contains(s)) ||
                (x.Barcode != null && x.Barcode.Contains(s)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "name" => request.SortDir == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
            "code" => request.SortDir == "asc" ? query.OrderBy(x => x.Code) : query.OrderByDescending(x => x.Code),
            "salePrice" => request.SortDir == "asc" ? query.OrderBy(x => x.SalePrice) : query.OrderByDescending(x => x.SalePrice),
            _ => query.OrderBy(x => x.Name)
        };

        var items = await query
            .Skip(request.StartRow)
            .Take(request.EndRow - request.StartRow)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                code = x.Code,
                categoryName = x.ProductCategory != null ? x.ProductCategory.Name : null,
                unitName = x.Unit.Name,
                salePrice = x.SalePrice,
                isActive = x.IsActive
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // GET: /Inventory/Products/Create
    [Authorize(Policy = "Product.Create")]
    public async Task<IActionResult> Create()
    {
        var model = new ProductFormViewModel
        {
            UnitOptions = await GetUnitOptionsAsync(),
            CategoryOptions = await GetCategoryOptionsAsync()
        };
        return View(model);
    }

    // POST: /Inventory/Products/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Product.Create")]
    public async Task<IActionResult> Create(ProductFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
        {
            model.UnitOptions = await GetUnitOptionsAsync();
            model.CategoryOptions = await GetCategoryOptionsAsync();
            return View(model);
        }

        var entity = new Product
        {
            TenantId = tenantId.Value,
            Name = model.Name.Trim(),
            Code = model.Code?.Trim(),
            Barcode = model.Barcode?.Trim(),
            Description = model.Description?.Trim(),
            UnitId = model.UnitId,
            ProductCategoryId = model.ProductCategoryId,
            LastPurchasePrice = model.LastPurchasePrice,
            SalePrice = model.SalePrice,
            TaxRate = model.TaxRate,
            TrackInventory = model.TrackInventory,
            IsActive = model.IsActive
        };

        _context.Products.Add(entity);
        await _context.SaveChangesAsync();

        TempData["Success"] = "کالا با موفقیت ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Inventory/Products/Edit/5
    [Authorize(Policy = "Product.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Products
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        var model = new ProductFormViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Barcode = entity.Barcode,
            Description = entity.Description,
            UnitId = entity.UnitId,
            ProductCategoryId = entity.ProductCategoryId,
            LastPurchasePrice = entity.LastPurchasePrice,
            SalePrice = entity.SalePrice,
            TaxRate = entity.TaxRate,
            TrackInventory = entity.TrackInventory,
            IsActive = entity.IsActive,
            UnitOptions = await GetUnitOptionsAsync(),
            CategoryOptions = await GetCategoryOptionsAsync()
        };

        return View(model);
    }

    // POST: /Inventory/Products/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Product.Edit")]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();
        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            model.UnitOptions = await GetUnitOptionsAsync();
            model.CategoryOptions = await GetCategoryOptionsAsync();
            return View(model);
        }

        var entity = await _context.Products
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        entity.Name = model.Name.Trim();
        entity.Code = model.Code?.Trim();
        entity.Barcode = model.Barcode?.Trim();
        entity.Description = model.Description?.Trim();
        entity.UnitId = model.UnitId;
        entity.ProductCategoryId = model.ProductCategoryId;
        entity.LastPurchasePrice = model.LastPurchasePrice;
        entity.SalePrice = model.SalePrice;
        entity.TaxRate = model.TaxRate;
        entity.TrackInventory = model.TrackInventory;

        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = "کالا با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Inventory/Products/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Product.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            var entity = await _context.Products
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

            if (entity == null)
                return Json(new { success = false, message = "کالا پیدا نشد." });

            if (entity.IsDeleted)
                return Json(new { success = false, message = "این کالا قبلاً حذف شده است." });

            // 🆕 چک فاکتور
            var hasInvoiceItems = await _context.InvoiceItems
                .AnyAsync(x => x.ProductId == id && !x.IsDeleted);

            if (hasInvoiceItems)
                return Json(new { success = false, message = "این کالا در فاکتورها استفاده شده و قابل حذف نیست." });

            // Soft Delete
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;

            // 🆕 Soft Delete تصاویر
            var images = await _context.ProductImages
                .IgnoreQueryFilters()
                .Where(i => i.ProductId == id && !i.IsDeleted)
                .ToListAsync();

            foreach (var img in images)
            {
                img.IsDeleted = true;
                img.DeletedAt = DateTime.UtcNow;
            }

            // 🆕 Soft Delete مشخصات
            var attributes = await _context.ProductAttributes
                .IgnoreQueryFilters()
                .Where(a => a.ProductId == id && !a.IsDeleted)
                .ToListAsync();

            foreach (var attr in attributes)
            {
                attr.IsDeleted = true;
                attr.DeletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "کالا با موفقیت حذف شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ---------- Helpers ----------

    private async Task<List<SelectListItem>> GetUnitOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Units
            .Where(x => x.TenantId == tenantId.Value && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetCategoryOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.ProductCategories
            .Where(x => x.TenantId == tenantId.Value && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }
}