using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Areas.Accounting.ViewModels.TransactionGroup;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class TransactionGroupsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public TransactionGroupsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // GET: /Accounting/TransactionGroups
    [Authorize(Policy = "TransactionGroup.View")]
    public async Task<IActionResult> Index()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var items = await _context.TransactionGroups
            .Where(x => x.TenantId == tenantId.Value)
            .Include(x => x.Parent)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new TransactionGroupListViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                ParentName = x.Parent != null ? x.Parent.Name : null,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return View(items);
    }

    // GET: /Accounting/TransactionGroups/Create
    [Authorize(Policy = "TransactionGroup.Create")]
    public async Task<IActionResult> Create()
    {
        var model = new TransactionGroupFormViewModel
        {
            ParentOptions = await GetParentOptionsAsync()
        };
        return View(model);
    }

    // POST: /Accounting/TransactionGroups/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TransactionGroup.Create")]
    public async Task<IActionResult> Create(TransactionGroupFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
        {
            model.ParentOptions = await GetParentOptionsAsync();
            return View(model);
        }

        var entity = new TransactionGroup
        {
            TenantId = tenantId.Value,
            Name = model.Name.Trim(),
            Code = model.Code?.Trim(),
            Description = model.Description?.Trim(),
            ParentId = model.ParentId,
            SortOrder = model.SortOrder,
            IsActive = model.IsActive
        };

        _context.TransactionGroups.Add(entity);
        await _context.SaveChangesAsync();

        TempData["Success"] = "گروه تراکنش با موفقیت ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Accounting/TransactionGroups/Edit/5
    [Authorize(Policy = "TransactionGroup.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.TransactionGroups
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        var model = new TransactionGroupFormViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            ParentId = entity.ParentId,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            ParentOptions = await GetParentOptionsAsync(entity.Id)
        };

        return View(model);
    }

    // POST: /Accounting/TransactionGroups/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TransactionGroup.Edit")]
    public async Task<IActionResult> Edit(int id, TransactionGroupFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            model.ParentOptions = await GetParentOptionsAsync(model.Id);
            return View(model);
        }

        var entity = await _context.TransactionGroups
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        entity.Name = model.Name.Trim();
        entity.Code = model.Code?.Trim();
        entity.Description = model.Description?.Trim();
        entity.ParentId = model.ParentId;
        entity.SortOrder = model.SortOrder;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = "گروه تراکنش با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Accounting/TransactionGroups/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TransactionGroup.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            var entity = await _context.TransactionGroups
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

            if (entity == null)
                return Json(new { success = false, message = "گروه تراکنش پیدا نشد." });

            if (entity.IsDeleted)
                return Json(new { success = false, message = "این گروه تراکنش قبلاً حذف شده است." });

            // 🆕 چک زیرگروه
            var hasChildren = await _context.TransactionGroups
                .AnyAsync(x => x.ParentId == id && !x.IsDeleted);

            if (hasChildren)
                return Json(new { success = false, message = "این گروه زیرگروه دارد و قابل حذف نیست." });

            // 🆕 چک تراکنش
            var hasTransactions = await _context.Transactions
                .AnyAsync(x => x.TransactionGroupId == id && !x.IsDeleted);

            if (hasTransactions)
                return Json(new { success = false, message = "این گروه در اسناد حسابداری استفاده شده و قابل حذف نیست." });

            // Soft Delete
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "گروه تراکنش با موفقیت حذف شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ---- Helper ----

    private async Task<List<TransactionGroupListItem>> GetParentOptionsAsync(int? excludeId = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        var query = _context.TransactionGroups
            .Where(x => x.TenantId == tenantId.Value && x.IsActive);

        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        return await query
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new TransactionGroupListItem
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();
    }

    // POST: /Accounting/TransactionGroups/GetData
    [HttpPost]
    [Authorize(Policy = "TransactionGroup.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.TransactionGroups
            .Where(x => x.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x =>
                (x.Name != null && x.Name.Contains(s)) ||
                (x.Code != null && x.Code.Contains(s)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "name" => request.SortDir == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
            "code" => request.SortDir == "asc" ? query.OrderBy(x => x.Code) : query.OrderByDescending(x => x.Code),
            "sortOrder" => request.SortDir == "asc" ? query.OrderBy(x => x.SortOrder) : query.OrderByDescending(x => x.SortOrder),
            _ => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
        };

        var items = await query
            .Skip(request.StartRow)
            .Take(request.EndRow - request.StartRow)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name,
                code = x.Code,
                parentName = x.Parent != null ? x.Parent.Name : null,
                sortOrder = x.SortOrder,
                isActive = x.IsActive
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }
}