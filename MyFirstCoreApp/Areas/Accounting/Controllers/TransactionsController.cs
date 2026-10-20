using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Accounting.ViewModels.Transaction;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class TransactionsController : Controller
{
    private const int MaxPageSize = 200;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITransactionService _transactionService;
    private readonly TimeProvider _timeProvider;

    public TransactionsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        ITransactionService transactionService,
        TimeProvider timeProvider)
    {
        _context = context;
        _currentTenant = currentTenant;
        _transactionService = transactionService;
        _timeProvider = timeProvider;
    }

    // ============================================
    // لیست
    // ============================================

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
            // کاربر ممکن است عدد را با ارقام فارسی تایپ کند
            var s = PersianDate.NormalizeDigits(request.Search.Trim());
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

        // سقف اندازه‌ی صفحه (جلوگیری از درخواست میلیونی)
        var startRow = Math.Max(request.StartRow, 0);
        var pageSize = Math.Clamp(request.EndRow - request.StartRow, 1, MaxPageSize);

        var rows = await query
            .Skip(startRow)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Number,
                x.Date,
                x.Description,
                GroupName = x.TransactionGroup != null ? x.TransactionGroup.Name : null,
                x.Reference,
                x.TotalAmount,
                x.IsPosted,
                x.ReferenceType,
                x.ReferenceId
            })
            .ToListAsync();

        // تبدیل تاریخ به شمسی سمت سرور (تکیه بر تنظیمات مرورگر/منطقه‌ی زمانی کاربر نیست)
        var items = rows.Select(x => new
        {
            id = x.Id,
            number = x.Number,
            date = PersianDate.Format(x.Date),
            description = x.Description,
            groupName = x.GroupName,
            reference = x.Reference,
            totalAmount = x.TotalAmount,
            isPosted = x.IsPosted,
            referenceType = x.ReferenceType,
            referenceId = x.ReferenceId
        });

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // ============================================
    // ثبت
    // ============================================

    // GET: /Accounting/Transactions/Create
    [Authorize(Policy = "Transaction.Create")]
    public async Task<IActionResult> Create()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var accountOptions = await GetAccountOptionsAsync();
        var contactOptions = await GetContactOptionsAsync();

        var model = new TransactionFormViewModel
        {
            // فقط نمایشی؛ شماره‌ی نهایی هنگام ذخیره تعیین می‌شود
            Number = await _transactionService.GetNextNumberAsync(tenantId.Value),
            Date = PersianDate.Format(_timeProvider.Today()),
            AccountOptions = accountOptions,
            ContactOptions = contactOptions,
            TransactionGroupOptions = await GetTransactionGroupOptionsAsync(),
            Lines = new List<TransactionLineViewModel>
            {
                new() { AccountOptions = accountOptions, ContactOptions = contactOptions, Credit = 0, Debit = 0 },
                new() { AccountOptions = accountOptions, ContactOptions = contactOptions, Credit = 0, Debit = 0 }
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

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

        if (!ModelState.IsValid)
            return await ReturnFormAsync(model);

        var transaction = new Transaction
        {
            TenantId = tenantId.Value,
            Number = 0, // خودکار
            Date = date,
            Description = model.Description?.Trim(),
            TransactionGroupId = model.TransactionGroupId,
            Reference = model.Reference?.Trim(),
            Lines = BuildLines(model, tenantId.Value)
        };

        try
        {
            await _transactionService.CreateAsync(transaction);
            TempData["Success"] = $"سند شماره {transaction.Number} با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await ReturnFormAsync(model);
        }
    }

    // ============================================
    // ویرایش
    // ============================================

    // GET: /Accounting/Transactions/Edit/5
    [Authorize(Policy = "Transaction.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _transactionService.GetByIdAsync(id, tenantId.Value);
        if (entity == null) return NotFound();

        if (!string.IsNullOrEmpty(entity.ReferenceType))
        {
            TempData["Error"] = "این سند خودکار است و از صفحه‌ی مربوطه باید مدیریت شود.";
            return RedirectToAction(nameof(Index));
        }

        if (entity.IsPosted)
        {
            TempData["Error"] = "سند قطعی قابل ویرایش نیست.";
            return RedirectToAction(nameof(Index));
        }

        var accountOptions = await GetAccountOptionsAsync();
        var contactOptions = await GetContactOptionsAsync();

        var model = new TransactionFormViewModel
        {
            Id = entity.Id,
            Number = entity.Number,
            Date = PersianDate.Format(entity.Date),
            Description = entity.Description,
            TransactionGroupId = entity.TransactionGroupId,
            Reference = entity.Reference,
            AttachmentPath = entity.AttachmentPath,
            AccountOptions = accountOptions,
            ContactOptions = contactOptions,
            TransactionGroupOptions = await GetTransactionGroupOptionsAsync(),
            Lines = entity.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new TransactionLineViewModel
                {
                    Id = l.Id,
                    AccountId = l.AccountId,
                    ContactId = l.ContactId,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Description = l.Description,
                    AccountOptions = accountOptions,
                    ContactOptions = contactOptions
                })
                .ToList()
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

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

        if (!ModelState.IsValid)
            return await ReturnFormAsync(model);

        var transaction = new Transaction
        {
            Id = id,
            TenantId = tenantId.Value,
            Date = date,
            Description = model.Description?.Trim(),
            TransactionGroupId = model.TransactionGroupId,
            Reference = model.Reference?.Trim(),
            AttachmentPath = model.AttachmentPath,
            Lines = BuildLines(model, tenantId.Value)
        };

        try
        {
            await _transactionService.UpdateAsync(transaction);
            TempData["Success"] = $"سند شماره {model.Number} با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await ReturnFormAsync(model);
        }
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
    // قطعی / پیش‌نویس / حذف  (AJAX)
    // ============================================

    // POST: /Accounting/Transactions/Post/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Transaction.Post")]
    public async Task<IActionResult> Post(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = NoTenantMessage });

        try
        {
            await _transactionService.PostAsync(id, tenantId.Value);
            return Json(new { success = true, message = "سند قطعی شد." });
        }
        catch (InvalidOperationException ex)
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
        if (!tenantId.HasValue) return Json(new { success = false, message = NoTenantMessage });

        try
        {
            await _transactionService.UnpostAsync(id, tenantId.Value);
            return Json(new { success = true, message = "سند به پیش‌نویس برگشت." });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Accounting/Transactions/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Transaction.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = NoTenantMessage });

        try
        {
            await _transactionService.DeleteManualAsync(id, tenantId.Value);
            return Json(new { success = true, message = "سند با موفقیت حذف شد." });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ============================================
    // Helpers
    // ============================================

    private const string InvalidDateMessage = "تاریخ نامعتبر است. نمونه‌ی درست: 1405/01/10";
    private const string NoTenantMessage = "شرکت فعال مشخص نیست.";

    /// <summary>
    /// خطوط خالی (بدون حساب یا بدون مبلغ) که کاربر در فرم رها کرده نادیده گرفته می‌شوند.
    /// بقیه‌ی قواعد در TransactionService اعمال می‌شود.
    /// </summary>
    private static List<TransactionLine> BuildLines(TransactionFormViewModel model, int tenantId)
    {
        return model.Lines
            .Where(l => l.AccountId > 0 && ((l.Debit ?? 0) > 0 || (l.Credit ?? 0) > 0))
            .Select(l => new TransactionLine
            {
                TenantId = tenantId,
                AccountId = l.AccountId,
                ContactId = l.ContactId,
                Debit = l.Debit ?? 0,
                Credit = l.Credit ?? 0,
                Description = l.Description?.Trim()
            })
            .ToList();
    }

    /// <summary>فرم را با Optionها پر می‌کند و View همین Action (Create یا Edit) را برمی‌گرداند.</summary>
    private async Task<IActionResult> ReturnFormAsync(TransactionFormViewModel model)
    {
        var accountOptions = await GetAccountOptionsAsync();
        var contactOptions = await GetContactOptionsAsync();

        model.AccountOptions = accountOptions;
        model.ContactOptions = contactOptions;
        model.TransactionGroupOptions = await GetTransactionGroupOptionsAsync();

        foreach (var line in model.Lines)
        {
            line.AccountOptions = accountOptions;
            line.ContactOptions = contactOptions;
        }

        return View(model);
    }

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
}