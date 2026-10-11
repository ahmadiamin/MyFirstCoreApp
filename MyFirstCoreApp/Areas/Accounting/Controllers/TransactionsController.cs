using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Accounting.ViewModels.Transaction;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class TransactionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITransactionService _transactionService;

    public TransactionsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        ITransactionService transactionService)
    {
        _context = context;
        _currentTenant = currentTenant;
        _transactionService = transactionService;
    }

    // GET: /Accounting/Transactions
    [Authorize(Policy = "Transaction.View")]
    public IActionResult Index() => View();

    // POST: /Accounting/Transactions/GetData
    [HttpPost]
    [Authorize(Policy = "Transaction.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Transactions
            .Where(x => x.TenantId == tenantId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x =>
                (x.Description != null && x.Description.Contains(s)) ||
                (x.Reference != null && x.Reference.Contains(s)) ||
                x.Number.ToString().Contains(s));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "number" => request.SortDir == "asc" ? query.OrderBy(x => x.Number) : query.OrderByDescending(x => x.Number),
            "date" => request.SortDir == "asc" ? query.OrderBy(x => x.Date) : query.OrderByDescending(x => x.Date),
            "totalAmount" => request.SortDir == "asc" ? query.OrderBy(x => x.TotalAmount) : query.OrderByDescending(x => x.TotalAmount),
            _ => query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Number)
        };

        var items = await query
            .Skip(request.StartRow)
            .Take(request.EndRow - request.StartRow)
            .Select(x => new
            {
                id = x.Id,
                number = x.Number,
                date = x.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                description = x.Description,
                groupName = x.TransactionGroup != null ? x.TransactionGroup.Name : null,
                reference = x.Reference,
                totalAmount = x.TotalAmount,
                isPosted = x.IsPosted,
                referenceType = x.ReferenceType,          
                referenceId = x.ReferenceId,              
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // GET: /Accounting/Transactions/Create
    [Authorize(Policy = "Transaction.Create")]
    public async Task<IActionResult> Create()
    {
        var accountOptions = await GetAccountOptionsAsync();
        var contactOptions = await GetContactOptionsAsync();
        var groupOptions = await GetTransactionGroupOptionsAsync();

        var model = new TransactionFormViewModel
        {
            Number = await _transactionService.GetNextNumberAsync(_currentTenant.TenantId ?? 0),
            Date = DateTime.Now.ToString("yyyy/MM/dd"),
            AccountOptions = accountOptions,
            ContactOptions = contactOptions,
            TransactionGroupOptions = groupOptions,
            Lines = new List<TransactionLineViewModel>
        {
            new() { AccountOptions = accountOptions, ContactOptions = contactOptions ,Credit = 0, Debit = 0 },
            new() { AccountOptions = accountOptions, ContactOptions = contactOptions ,Credit = 0, Debit = 0 }
        }
        };
        return View(model);
    }

    // POST: /Accounting/Transactions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Transaction.Create")]
    public async Task<IActionResult> Create(TransactionFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        // چک خطوط
        var validLines = model.Lines
            .Where(l => l.AccountId > 0 && (l.Debit > 0 || l.Credit > 0))
            .ToList();

        if (validLines.Count < 2)
        {
            ModelState.AddModelError("", "حداقل ۲ خط با حساب و مبلغ لازم است.");
        }

        // چک مجموع
        var totalDebit = validLines.Sum(l => l.Debit ?? 0);
        var totalCredit = validLines.Sum(l => l.Credit ?? 0);

        if (totalDebit != totalCredit)
        {
            ModelState.AddModelError("", $"مجموع بدهکار ({totalDebit:N0}) با مجموع بستانکار ({totalCredit:N0}) برابر نیست.");
        }

        if (!ModelState.IsValid)
        {
            var accountOptions = await GetAccountOptionsAsync();
            var contactOptions = await GetContactOptionsAsync();
            var groupOptions = await GetTransactionGroupOptionsAsync();

            model.AccountOptions = accountOptions;
            model.ContactOptions = contactOptions;
            model.TransactionGroupOptions = groupOptions;

            // 🆕 پر کردن Options هر خط
            foreach (var line in model.Lines)
            {
                line.AccountOptions = accountOptions;
                line.ContactOptions = contactOptions;
            }

            return View(model);
        }

        // تبدیل تاریخ شمسی به میلادی
        var date = ParsePersianDate(model.Date);

        var transaction = new Transaction
        {
            TenantId = tenantId.Value,
            Number = model.Number,
            Date = date,
            Description = model.Description?.Trim(),
            TransactionGroupId = model.TransactionGroupId,
            Reference = model.Reference?.Trim(),
            Lines = validLines.Select(l => new TransactionLine
            {
                TenantId = tenantId.Value,
                AccountId = l.AccountId,
                ContactId = l.ContactId,
                Debit = l.Debit ?? 0,
                Credit = l.Credit ?? 0,
                Description = l.Description?.Trim()
            }).ToList()
        };

        try
        {
            await _transactionService.CreateAsync(transaction);
            TempData["Success"] = $"سند شماره {transaction.Number} با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            model.AccountOptions = await GetAccountOptionsAsync();
            model.ContactOptions = await GetContactOptionsAsync();
            model.TransactionGroupOptions = await GetTransactionGroupOptionsAsync();
            return View(model);
        }
    }

    // ---------- Helpers ----------

    private async Task<List<SelectListItem>> GetAccountOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Accounts
            .Where(x => x.TenantId == tenantId.Value && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetContactOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Contacts
            .Where(x => x.TenantId == tenantId.Value && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetTransactionGroupOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.TransactionGroups
            .Where(x => x.TenantId == tenantId.Value && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }

    private DateTime ParsePersianDate(string persianDate)
    {
        // TODO: تبدیل شمسی به میلادی
        // فعلاً فرض کن کاربر میلادی وارد کرده
        if (DateTime.TryParse(persianDate, out var dt))
            return dt;
        return DateTime.UtcNow;
    }

    // POST: /Accounting/Transactions/Post/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Transaction.Post")]
    public async Task<IActionResult> Post(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            var entity = await _context.Transactions
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

            if (entity == null)
                return Json(new { success = false, message = "سند پیدا نشد." });

            // 🆕 چک سند خودکار
            if (!string.IsNullOrEmpty(entity.ReferenceType))
                return Json(new { success = false, message = "این سند خودکار است و از صفحه‌ی مربوطه باید مدیریت شود." });

            entity.IsPosted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "سند قطعی شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Accounting/Transactions/Unpost/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Transaction.Post")]
    public async Task<IActionResult> Unpost(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Transactions
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (!entity.IsPosted)
        {
            return Json(new { success = false, message = "این سند پیش‌نویس است." });
        }

        entity.IsPosted = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"سند شماره {entity.Number} به پیش‌نویس برگشت." });
    }

    // ============================================
    // Details
    // ============================================

    // GET: /Accounting/Transactions/Details/5
    [Authorize(Policy = "Transaction.View")]
    public async Task<IActionResult> Details(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Transactions
            .Include(t => t.TransactionGroup)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Account)
            .Include(t => t.Lines)
                .ThenInclude(l => l.Contact)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        return View(entity);
    }

    // ============================================
    // Edit
    // ============================================

    // GET: /Accounting/Transactions/Edit/5
    [Authorize(Policy = "Transaction.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Transactions
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (entity.IsPosted)
        {
            TempData["Error"] = "سند قطعی قابل ویرایش نیست.";
            return RedirectToAction(nameof(Index));
        }

        var accountOptions = await GetAccountOptionsAsync();
        var contactOptions = await GetContactOptionsAsync();
        var groupOptions = await GetTransactionGroupOptionsAsync();

        var model = new TransactionFormViewModel
        {
            Id = entity.Id,
            Number = entity.Number,
            Date = entity.Date.ToString("yyyy/MM/dd"),
            Description = entity.Description,
            TransactionGroupId = entity.TransactionGroupId,
            Reference = entity.Reference,
            AttachmentPath = entity.AttachmentPath,
            AccountOptions = accountOptions,
            ContactOptions = contactOptions,
            TransactionGroupOptions = groupOptions,
            Lines = entity.Lines.Select(l => new TransactionLineViewModel
            {
                Id = l.Id,
                AccountId = l.AccountId,
                ContactId = l.ContactId,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description,
                AccountOptions = accountOptions,
                ContactOptions = contactOptions
            }).ToList()
        };

        return View(model);
    }

    // POST: /Accounting/Transactions/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Transaction.Edit")]
    public async Task<IActionResult> Edit(int id, TransactionFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        // چک خطوط
        var validLines = model.Lines
           .Where(l => l.AccountId > 0 && ((l.Debit ?? 0) > 0 || (l.Credit ?? 0) > 0))
           .ToList();

        if (validLines.Count < 2)
        {
            ModelState.AddModelError("", "حداقل ۲ خط با حساب و مبلغ لازم است.");
        }

        // چک مجموع
        var totalDebit = validLines.Sum(l => l.Debit);
        var totalCredit = validLines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
        {
            ModelState.AddModelError("", $"مجموع بدهکار ({totalDebit:N0}) با مجموع بستانکار ({totalCredit:N0}) برابر نیست.");
        }

        if (!ModelState.IsValid)
        {
            var accountOptions = await GetAccountOptionsAsync();
            var contactOptions = await GetContactOptionsAsync();
            var groupOptions = await GetTransactionGroupOptionsAsync();

            model.AccountOptions = accountOptions;
            model.ContactOptions = contactOptions;
            model.TransactionGroupOptions = groupOptions;

            // 🆕 پر کردن Options هر خط
            foreach (var line in model.Lines)
            {
                line.AccountOptions = accountOptions;
                line.ContactOptions = contactOptions;
            }

            return View(model);
        }

        var entity = await _context.Transactions
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (entity.IsPosted)
        {
            TempData["Error"] = "سند قطعی قابل ویرایش نیست.";
            return RedirectToAction(nameof(Index));
        }

        // به‌روزرسانی هدر
        entity.Date = ParsePersianDate(model.Date);
        entity.Description = model.Description?.Trim();
        entity.TransactionGroupId = model.TransactionGroupId;
        entity.Reference = model.Reference?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.TotalAmount = totalDebit ?? 0;

        // حذف خطوط قدیمی
        _context.TransactionLines.RemoveRange(entity.Lines);

        // اضافه کردن خطوط جدید
        var lineNumber = 1;
        foreach (var l in validLines)
        {
            entity.Lines.Add(new TransactionLine
            {
                TenantId = tenantId.Value,
                LineNumber = lineNumber++,
                AccountId = l.AccountId,
                ContactId = l.ContactId,
                Debit = l.Debit ?? 0,
                Credit = l.Credit ?? 0,
                Description = l.Description?.Trim()
            });
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"سند شماره {entity.Number} با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // Delete
    // ============================================

    // POST: /Accounting/Transactions/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Transaction.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            var entity = await _context.Transactions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

            if (entity == null)
                return Json(new { success = false, message = "سند پیدا نشد." });

            if (entity.IsDeleted)
                return Json(new { success = false, message = "این سند قبلاً حذف شده است." });

            // 🆕 چک سند خودکار
            if (!string.IsNullOrEmpty(entity.ReferenceType))
            {
                var refName = entity.ReferenceType switch
                {
                    "Invoice" => "فاکتور",
                    "Cheque" => "چک",
                    "Payment" => "پرداخت",
                    _ => "مرجع"
                };
                return Json(new { success = false, message = $"این سند خودکار است و از صفحه‌ی {refName} باید مدیریت شود." });
            }

            // چک قطعی
            if (entity.IsPosted)
                return Json(new { success = false, message = "سند قطعی قابل حذف نیست. ابتدا آن را به پیش‌نویس برگردانید." });

            // Soft Delete
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;

            // Soft Delete خطوط
            var lines = await _context.TransactionLines
                .IgnoreQueryFilters()
                .Where(l => l.TransactionId == id && !l.IsDeleted)
                .ToListAsync();

            foreach (var line in lines)
            {
                line.IsDeleted = true;
                line.DeletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "سند با موفقیت حذف شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}