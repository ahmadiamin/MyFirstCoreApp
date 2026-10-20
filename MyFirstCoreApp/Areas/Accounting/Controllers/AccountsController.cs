using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Areas.Accounting.ViewModels.Account;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Extensions;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class AccountsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public AccountsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    // GET: /Accounting/Accounts
    [Authorize(Policy = "Account.View")]
    public async Task<IActionResult> Index()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var items = await _context.Accounts
            .Where(x => x.TenantId == tenantId.Value)
            .Include(x => x.AccountCategory)
            .OrderBy(x => x.AccountCategory.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new AccountListViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                CategoryName = x.AccountCategory.Name,
                AccountTypeName = GetAccountTypeName(x.AccountType),
                BankName = x.BankName,
                AccountNumber = x.AccountNumber,
                CurrentBalance = x.CurrentBalance,
                IsActive = x.IsActive,
                IsDefault = x.IsDefault
            })
            .ToListAsync();

        return View(items);
    }

    // GET: /Accounting/Accounts/Create
    [Authorize(Policy = "Account.Create")]
    public async Task<IActionResult> Create()
    {
        var model = new AccountFormViewModel
        {
            CategoryOptions = await GetCategoryOptionsAsync(),
            AccountTypeOptions = EnumExtensions.GetEnumSelectListWithDisplay<AccountType>()
        };
        return View(model);
    }

    // POST: /Accounting/Accounts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Account.Create")]
    public async Task<IActionResult> Create(AccountFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await GetCategoryOptionsAsync();
            model.AccountTypeOptions = EnumExtensions.GetEnumSelectListWithDisplay<AccountType>();
            return View(model);
        }

        // اگه IsDefault = true، بقیه‌ی حساب‌های هم‌نوع رو از پیش‌فرض بودن دربیار
        if (model.IsDefault)
        {
            var defaults = await _context.Accounts
                .Where(x => x.TenantId == tenantId.Value && x.AccountType == model.AccountType && x.IsDefault)
                .ToListAsync();
            foreach (var d in defaults) d.IsDefault = false;
        }

        var entity = new Account
        {
            TenantId = tenantId.Value,
            Name = model.Name.Trim(),
            Code = model.Code?.Trim(),
            Description = model.Description?.Trim(),
            AccountCategoryId = model.AccountCategoryId,
            AccountType = model.AccountType,
            BankName = model.BankName?.Trim(),
            AccountNumber = model.AccountNumber?.Trim(),
            CardNumber = model.CardNumber?.Trim(),
            ShebaNumber = model.ShebaNumber?.Replace("IR","").Trim(),
            CurrentBalance = model.CurrentBalance,
            IsDefault = model.IsDefault,
            IsActive = model.IsActive
        };

        _context.Accounts.Add(entity);
        await _context.SaveChangesAsync();

        TempData["Success"] = "حساب با موفقیت ثبت شد.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /Accounting/Accounts/Edit/5
    [Authorize(Policy = "Account.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Accounts
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        var model = new AccountFormViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            AccountCategoryId = entity.AccountCategoryId,
            AccountType = entity.AccountType,
            BankName = entity.BankName,
            AccountNumber = entity.AccountNumber,
            CardNumber = entity.CardNumber,
            ShebaNumber = entity.ShebaNumber?.Replace("IR", "").Trim(),
            CurrentBalance = entity.CurrentBalance,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            CategoryOptions = await GetCategoryOptionsAsync(),
            AccountTypeOptions = EnumExtensions.GetEnumSelectListWithDisplay<AccountType>()
        };

        return View(model);
    }

    // POST: /Accounting/Accounts/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Account.Edit")]
    public async Task<IActionResult> Edit(int id, AccountFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            model.CategoryOptions = await GetCategoryOptionsAsync();
            model.AccountTypeOptions = EnumExtensions.GetEnumSelectListWithDisplay<AccountType>();
            return View(model);
        }

        var entity = await _context.Accounts
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        // اگه IsDefault = true، بقیه‌ی حساب‌های هم‌نوع رو از پیش‌فرض بودن دربیار
        if (model.IsDefault && !entity.IsDefault)
        {
            var defaults = await _context.Accounts
                .Where(x => x.TenantId == tenantId.Value
                         && x.AccountType == model.AccountType
                         && x.IsDefault
                         && x.Id != id)
                .ToListAsync();
            foreach (var d in defaults) d.IsDefault = false;
        }

        entity.Name = model.Name.Trim();
        entity.Code = model.Code?.Trim();
        entity.Description = model.Description?.Trim();
        entity.AccountCategoryId = model.AccountCategoryId;
        entity.AccountType = model.AccountType;
        entity.BankName = model.BankName?.Trim();
        entity.AccountNumber = model.AccountNumber?.Trim();
        entity.CardNumber = model.CardNumber?.Trim();
        entity.ShebaNumber = model.ShebaNumber?.Trim();
        entity.CurrentBalance = model.CurrentBalance;
        entity.IsDefault = model.IsDefault;
        entity.IsActive = model.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["Success"] = "حساب با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }
    // POST: /Accounting/Accounts/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Account.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            var entity = await _context.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

            if (entity == null)
                return Json(new { success = false, message = "حساب پیدا نشد." });

            if (entity.IsDeleted)
                return Json(new { success = false, message = "این حساب قبلاً حذف شده است." });

            // 🆕 چک زیرحساب
            var hasChildren = await _context.Accounts
                .AnyAsync(x => x.ParentId == id && !x.IsDeleted);

            if (hasChildren)
                return Json(new { success = false, message = "این حساب زیرحساب دارد و قابل حذف نیست." });

            // 🆕 چک تراکنش
            var hasTransactions = await _context.TransactionLines
                .AnyAsync(x => x.AccountId == id && !x.IsDeleted);

            if (hasTransactions)
                return Json(new { success = false, message = "این حساب در اسناد حسابداری استفاده شده و قابل حذف نیست." });

            // Soft Delete
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "حساب با موفقیت حذف شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    // ---- Helper ----

    private async Task<List<SelectListItem>> GetCategoryOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.AccountCategories
            .Where(x => x.TenantId == tenantId.Value && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }

    private static string GetAccountTypeName(AccountType type)
    {
        return type switch
        {
            AccountType.Bank => "بانکی",
            AccountType.Cash => "نقدی",
            AccountType.Receivable => "دریافتنی",
            AccountType.Payable => "پرداختنی",
            AccountType.Capital => "سرمایه",
            AccountType.Income => "درآمد",
            AccountType.Expense => "هزینه",
            AccountType.Contra => "واسط",
            _ => "-"
        };
    }

    // POST: /Accounting/Accounts/GetData
    [HttpPost]
    [Authorize(Policy = "Account.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Accounts
            .Where(x => x.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x =>
                (x.Name != null && x.Name.Contains(s)) ||
                (x.Code != null && x.Code.Contains(s)) ||
                (x.BankName != null && x.BankName.Contains(s)) ||
                (x.AccountNumber != null && x.AccountNumber.Contains(s)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "name" => request.SortDir == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
            "currentBalance" => request.SortDir == "asc" ? query.OrderBy(x => x.CurrentBalance) : query.OrderByDescending(x => x.CurrentBalance),
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
                categoryName = x.AccountCategory.Name,
                accountTypeName = GetAccountTypeName(x.AccountType),
                bankName = x.BankName,
                accountNumber = x.AccountNumber,
                currentBalance = x.CurrentBalance,
                isDefault = x.IsDefault,
                isActive = x.IsActive
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }



}