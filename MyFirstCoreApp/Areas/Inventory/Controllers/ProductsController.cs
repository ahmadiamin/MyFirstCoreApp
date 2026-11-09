using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Areas.Inventory.ViewModels.Product;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Data;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Application.Common.Utilities;
namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStockMovementService _stockMovementService;

    public ProductsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IStockMovementService stockMovementService  )
    {
        _context = context;
        _currentTenant = currentTenant;
        _stockMovementService = stockMovementService;
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
    public async Task<IActionResult> Create()
    {
        var model = new ProductFormViewModel
        {
            UnitOptions = await GetUnitOptionsAsync(),
            CategoryOptions = await GetCategoryOptionsAsync(),
            WarehouseOptions = await GetWarehouseOptionsAsync()
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

        if (!tenantId.HasValue)
            return Forbid();

        // اعتبارسنجی موجودی اولیه
        if (!model.TrackInventory && model.OpeningQuantity > 0)
        {
            ModelState.AddModelError(
                nameof(model.OpeningQuantity),
                "برای خدمات امکان ثبت موجودی اولیه وجود ندارد.");
        }

        if (model.OpeningQuantity > 0)
        {
            if (!model.OpeningWarehouseId.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.OpeningWarehouseId),
                    "برای ثبت موجودی اولیه، انتخاب انبار الزامی است.");
            }

            if (!model.IsActive)
            {
                ModelState.AddModelError(
                    nameof(model.IsActive),
                    "برای ثبت موجودی اولیه، کالا باید فعال باشد.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.UnitOptions = await GetUnitOptionsAsync();
            model.CategoryOptions = await GetCategoryOptionsAsync();
            model.WarehouseOptions = await GetWarehouseOptionsAsync();

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
            MinimumStock = model.MinimumStock,
            TaxRate = model.TaxRate,
            TrackInventory = model.TrackInventory,
            IsActive = model.IsActive
        };

        try
        {
            await _context.ExecuteInTransactionAsync(
                IsolationLevel.Serializable,
                async () =>
                {
                    // 1. ثبت کالا
                    _context.Products.Add(entity);

                    await _context.SaveChangesAsync();

                    // 2. در صورت داشتن موجودی اولیه، ثبت سند افتتاحیه
                    if (model.TrackInventory &&
                        model.OpeningQuantity > 0)
                    {
                        var movementRequest = new StockMovementRequest
                        {
                            Date = DateOnly.FromDateTime(DateTime.Today),

                            MovementType = (int)StockMovementType.Opening,

                            SourceWarehouseId = null,

                            DestinationWarehouseId =
                                model.OpeningWarehouseId,

                            Description =
                                $"موجودی اولیه هنگام تعریف کالا: {entity.Name}",

                            ReferenceInvoiceId = null,

                            Items = new List<StockMovementItemRequest>
                            {
                            new()
                            {
                                ProductId = entity.Id,
                                Quantity = model.OpeningQuantity,
                                UnitCost = model.OpeningUnitCost,
                                Description = "موجودی افتتاحیه"
                            }
                            }
                        };

                        // ایجاد سند پیش‌نویس
                        var movementId =
                            await _stockMovementService.CreateAsync(
                                movementRequest,
                                tenantId.Value);

                        // قطعی کردن سند
                        await _stockMovementService.PostAsync(
                            movementId,
                            tenantId.Value);
                    }
                });

            TempData["Success"] = model.OpeningQuantity > 0
                ? "کالا و موجودی اولیه آن با موفقیت ثبت شدند."
                : "کالا با موفقیت ثبت شد.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(
                string.Empty,
                ex.Message);

            model.UnitOptions = await GetUnitOptionsAsync();
            model.CategoryOptions = await GetCategoryOptionsAsync();
            model.WarehouseOptions = await GetWarehouseOptionsAsync();

            return View(model);
        }
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
            MinimumStock = entity.MinimumStock,
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
        entity.MinimumStock = model.MinimumStock;
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

    private async Task<List<SelectListItem>> GetWarehouseOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;

        if (!tenantId.HasValue)
            return new();

        return await _context.Warehouses
            .Where(x =>
                x.TenantId == tenantId.Value &&
                x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }
}
