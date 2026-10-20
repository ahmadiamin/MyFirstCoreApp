using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Areas.Accounting.ViewModels.AccountCategory;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class AccountCategoriesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public AccountCategoriesController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // GET: /Accounting/AccountCategories
    [Authorize(Policy = "AccountCategory.View")]
    public async Task<IActionResult> Index()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var items = await _context.AccountCategories
            .Where(x => x.TenantId == tenantId.Value)
            .Include(x => x.Parent)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new AccountCategoryListViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Description = x.Description,
                ParentName = x.Parent != null ? x.Parent.Name : null,
                DefaultAccountTypeName = GetAccountTypeName(x.DefaultAccountType),
                IsActive = x.IsActive,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return View(items);
    }

    // GET: /Accounting/AccountCategories/Create
    [Authorize(Policy = "AccountCategory.Create")]
    public async Task<IActionResult> Create()
    {
        var model = new AccountCategoryFormViewModel
        {
            ParentOptions = await GetParentOptionsAsync()
        };
        return View(model);
    }

    // POST: /Accounting/AccountCategories/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AccountCategory.Create")]
    public async Task<IActionResult> Create(AccountCategoryFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
        {
            model.ParentOptions = await GetParentOptionsAsync();
            return View(model);
        }

        var entity = new AccountCategory
        {
            TenantId = tenantId.Value,
            Name = model.Name.Trim(),
            Code = model.Code?.Trim(),
            Description = model.Description?.Trim(),
            ParentId = model.ParentId,
            DefaultAccountType = model.DefaultAccountType,
            SortOrder = model.SortOrder,
            IsActive = model.IsActive
        };

        _context.AccountCategories.Add(entity);
        await _context.SaveChangesAsync();

        TempData["Success"] = "دسته‌بندی حساب با موفقیت ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Accounting/AccountCategories/Edit/5
    [Authorize(Policy = "AccountCategory.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.AccountCategories
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        var model = new AccountCategoryFormViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            ParentId = entity.ParentId,
            DefaultAccountType = entity.DefaultAccountType,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            ParentOptions = await GetParentOptionsAsync(entity.Id)
        };

        return View(model);
    }

    // POST: /Accounting/AccountCategories/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AccountCategory.Edit")]
    public async Task<IActionResult> Edit(int id, AccountCategoryFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            model.ParentOptions = await GetParentOptionsAsync(model.Id);
            return View(model);
        }

        var entity = await _context.AccountCategories
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        entity.Name = model.Name.Trim();
        entity.Code = model.Code?.Trim();
        entity.Description = model.Description?.Trim();
        entity.ParentId = model.ParentId;
        entity.DefaultAccountType = model.DefaultAccountType;
        entity.SortOrder = model.SortOrder;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = "دسته‌بندی حساب با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Accounting/AccountCategories/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AccountCategory.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            var entity = await _context.AccountCategories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

            if (entity == null)
                return Json(new { success = false, message = "دسته‌بندی پیدا نشد." });

            if (entity.IsDeleted)
                return Json(new { success = false, message = "این دسته‌بندی قبلاً حذف شده است." });

            // 🆕 چک زیردسته
            var hasChildren = await _context.AccountCategories
                .AnyAsync(x => x.ParentId == id && !x.IsDeleted);

            if (hasChildren)
                return Json(new { success = false, message = "این دسته‌بندی زیردسته دارد و قابل حذف نیست." });

            // 🆕 چک حساب
            var hasAccounts = await _context.Accounts
                .AnyAsync(x => x.AccountCategoryId == id && !x.IsDeleted);

            if (hasAccounts)
                return Json(new { success = false, message = "این دسته‌بندی حساب دارد و قابل حذف نیست." });

            // Soft Delete
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "دسته‌بندی با موفقیت حذف شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ---- Helper ----

    private async Task<List<AccountCategoryListItem>> GetParentOptionsAsync(int? excludeId = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new List<AccountCategoryListItem>();

        var query = _context.AccountCategories
            .Where(x => x.TenantId == tenantId.Value && x.IsActive);

        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        return await query
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new AccountCategoryListItem
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();
    }

    private static string GetAccountTypeName(Domain.Enums.AccountType type)
    {
        return type switch
        {
            Domain.Enums.AccountType.Bank => "بانکی",
            Domain.Enums.AccountType.Cash => "نقدی",
            Domain.Enums.AccountType.Receivable => "دریافتنی",
            Domain.Enums.AccountType.Payable => "پرداختنی",
            Domain.Enums.AccountType.Capital => "سرمایه",
            Domain.Enums.AccountType.Income => "درآمد",
            Domain.Enums.AccountType.Expense => "هزینه",
            Domain.Enums.AccountType.Contra => "واسط",
            _ => "-"
        };
    }

    // POST: /Accounting/AccountCategories/GetData
    [HttpPost]
    [Authorize(Policy = "AccountCategory.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.AccountCategories
            .Where(x => x.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x =>
                (x.Name != null && x.Name.Contains(s)) ||
                (x.Code != null && x.Code.Contains(s)) ||
                (x.Description != null && x.Description.Contains(s)));
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
                accountTypeName = GetAccountTypeName(x.DefaultAccountType),
                sortOrder = x.SortOrder,
                isActive = x.IsActive
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }
}