using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Projects;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Areas.Projects.ViewModels;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting"), Authorize]
public class ExpensesController : Controller
{
    private readonly IProjectExpenseService _service;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly TimeProvider _timeProvider;
    private readonly IAuthorizationService _authorization;
    public ExpensesController(IProjectExpenseService service, ApplicationDbContext db, ICurrentTenant tenant, TimeProvider timeProvider, IAuthorizationService authorization)
    { _service = service; _db = db; _tenant = tenant; _timeProvider = timeProvider; _authorization = authorization; }

    [Authorize(Policy = "ProjectExpense.View")]
    public async Task<IActionResult> Index(int? projectId)
    {
        ViewBag.ProjectId = projectId;
        ViewBag.CanDelete = (await _authorization.AuthorizeAsync(User, null, "ProjectExpense.Delete")).Succeeded;
        var items = await _service.GetListAsync(RequireTenant(), projectId);
        ViewBag.InitialRows = items.Select(x => new
        {
            id = x.Id, number = x.Number, date = PersianDate.Format(x.Date, true), projectId = x.ProjectId,
            projectTitle = x.ProjectTitle, contactName = x.ContactName, description = x.Description,
            amount = x.Amount, paidAmount = x.PaidAmount, remainingAmount = x.RemainingAmount, reference = x.Reference
        }).ToList();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "ProjectExpense.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request, int? projectId)
    {
        var tenantId = _tenant.TenantId;
        if (!tenantId.HasValue) return Forbid();
        request ??= new AgGridRequest();
        var items = await _service.GetListAsync(tenantId.Value, projectId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = PersianDate.NormalizeDigits(request.Search.Trim());
            items = items.Where(x => x.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.Reference?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.ProjectTitle?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.ContactName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Number.ToString().Contains(term, StringComparison.Ordinal)).ToList();
        }
        var total = items.Count;
        items = request.SortBy switch
        {
            "number" => request.SortDir == "asc" ? items.OrderBy(x => x.Number).ToList() : items.OrderByDescending(x => x.Number).ToList(),
            "date" => request.SortDir == "asc" ? items.OrderBy(x => x.Date).ToList() : items.OrderByDescending(x => x.Date).ToList(),
            "amount" => request.SortDir == "asc" ? items.OrderBy(x => x.Amount).ToList() : items.OrderByDescending(x => x.Amount).ToList(),
            "paidAmount" => request.SortDir == "asc" ? items.OrderBy(x => x.PaidAmount).ToList() : items.OrderByDescending(x => x.PaidAmount).ToList(),
            "remainingAmount" => request.SortDir == "asc" ? items.OrderBy(x => x.RemainingAmount).ToList() : items.OrderByDescending(x => x.RemainingAmount).ToList(),
            "projectTitle" => request.SortDir == "asc" ? items.OrderBy(x => x.ProjectTitle).ToList() : items.OrderByDescending(x => x.ProjectTitle).ToList(),
            "contactName" => request.SortDir == "asc" ? items.OrderBy(x => x.ContactName).ToList() : items.OrderByDescending(x => x.ContactName).ToList(),
            _ => items.OrderByDescending(x => x.Date).ThenByDescending(x => x.Number).ToList()
        };
        var pageSize = Math.Clamp(request.EndRow - request.StartRow, 1, 10000);
        var data = items.Skip(Math.Max(0, request.StartRow)).Take(pageSize).ToList();
        var canDelete = (await _authorization.AuthorizeAsync(User, null, "ProjectExpense.Delete")).Succeeded;
        var rows = data.Select(x => new { id = x.Id, number = x.Number, date = PersianDate.Format(x.Date, true),
            projectId = x.ProjectId, projectTitle = x.ProjectTitle, contactName = x.ContactName, description = x.Description,
            amount = x.Amount, paidAmount = x.PaidAmount, remainingAmount = x.RemainingAmount, reference = x.Reference, canDelete });
        return Json(new { success = true, rows, lastRow = total });
    }

    [Authorize(Policy = "ProjectExpense.Create")]
    public async Task<IActionResult> Create(int? projectId)
    {
        var defaultExpenseAccountId = await LoadOptions();
        return View(new ProjectExpenseFormViewModel { ProjectId = projectId, ExpenseAccountId = defaultExpenseAccountId, DateText = PersianDate.Format(_timeProvider.Today()) });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "ProjectExpense.Create")]
    public async Task<IActionResult> Create(ProjectExpenseFormViewModel model)
    {
        var hasDate = PersianDate.TryParse(model.DateText, out var date);
        if (!hasDate) ModelState.AddModelError(nameof(model.DateText), "تاریخ شمسی معتبر نیست.");
        var hasAmount = TryParseAmount(model.AmountText, out var amount);
        if (!hasAmount) ModelState.AddModelError(nameof(model.AmountText), "مبلغ را به‌صورت عدد صحیح و بزرگ‌تر از صفر وارد کنید.");
        if (!ModelState.IsValid) { await LoadOptions(model.ExpenseAccountId); return View(model); }
        try
        {
            var id = await _service.CreateAsync(new ProjectExpenseRequest { Date = date, ProjectId = model.ProjectId, ContactId = model.ContactId,
                ExpenseAccountId = model.ExpenseAccountId, Amount = amount, Description = model.Description, Reference = model.Reference }, RequireTenant());
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        await LoadOptions(model.ExpenseAccountId); return View(model);
    }

    [Authorize(Policy = "ProjectExpense.View")]
    public async Task<IActionResult> Details(int id)
    {
        var dto = await _service.GetByIdAsync(id, RequireTenant());
        if (dto == null) return NotFound();
        ViewBag.CanDeleteExpense = (await _authorization.AuthorizeAsync(User, null, "ProjectExpense.Delete")).Succeeded;
        ViewBag.CashAccounts = new SelectList(await _db.Accounts.Where(a => a.IsActive && (a.AccountType == AccountType.Cash || a.AccountType == AccountType.Bank)).OrderBy(a => a.Name).Select(a => new { a.Id, a.Name }).ToListAsync(), "Id", "Name");
        ViewBag.PaymentMethods = new SelectList(new[]
        {
            new { Id = (int)PaymentMethod.Cash, Name = "نقدی" },
            new { Id = (int)PaymentMethod.BankTransfer, Name = "انتقال بانکی" },
            new { Id = (int)PaymentMethod.Card, Name = "کارت بانکی" },
            new { Id = (int)PaymentMethod.Pos, Name = "کارت‌خوان" },
            new { Id = (int)PaymentMethod.Internet, Name = "پرداخت اینترنتی" },
            new { Id = (int)PaymentMethod.Other, Name = "سایر" }
        }, "Id", "Name");
        return View(dto);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "ProjectExpense.Create")]
    public async Task<IActionResult> AddPayment(ProjectExpensePaymentFormViewModel model)
    {
        if (!PersianDate.TryParse(model.DateText, out var date)) TempData["Error"] = "تاریخ شمسی معتبر نیست.";
        else if (!TryParseAmount(model.AmountText, out var amount)) TempData["Error"] = "مبلغ را به‌صورت عدد صحیح و بزرگ‌تر از صفر وارد کنید.";
        else
        {
            try { await _service.AddPaymentAsync(new ProjectExpensePaymentRequest { ProjectExpenseId = model.ProjectExpenseId, Date = date, Amount = amount, AccountId = model.AccountId,
                PaymentMethod = model.PaymentMethod, Reference = model.Reference, Description = model.Description }, RequireTenant()); TempData["Success"] = "پرداخت ثبت شد."; }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        }
        return RedirectToAction(nameof(Details), new { id = model.ProjectExpenseId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "ProjectExpense.Delete")]
    public async Task<IActionResult> DeletePayment(int expenseId, int paymentId)
    {
        try { await _service.DeletePaymentAsync(expenseId, paymentId, RequireTenant()); TempData["Success"] = "پرداخت و سند حسابداری مرتبط حذف نرم شدند."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = expenseId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "ProjectExpense.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var projectId = await _db.ProjectExpenses.Where(x => x.Id == id).Select(x => x.ProjectId).FirstOrDefaultAsync();
        try { await _service.DeleteAsync(id, RequireTenant()); TempData["Success"] = "هزینه، پرداخت‌های آن و اسناد حسابداری مرتبط حذف نرم شدند."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index), new { projectId });
    }

    private int RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("شرکت فعال مشخص نیست.");
    private async Task<int> LoadOptions(int? selectedExpenseAccountId = null)
    {
        ViewBag.Projects = new SelectList(await _db.Projects.OrderBy(p => p.Title).Select(p => new { p.Id, p.Title }).ToListAsync(), "Id", "Title");
        ViewBag.Contacts = new SelectList(await _db.Contacts.Where(c => c.IsActive).OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(), "Id", "Name");
        // این سه حساب برای ثبت خودکار فاکتورها استفاده می‌شوند، نه هزینه‌های عمومی.
        var excludedSystemNames = new[] { "برگشت از فروش", "تخفیف فروش", "خرید کالا" };
        var expenseAccounts = await _db.Accounts.Where(a => a.IsActive && a.AccountType == AccountType.Expense && !excludedSystemNames.Contains(a.Name))
            .OrderBy(a => a.Name).Select(a => new { a.Id, a.Name }).ToListAsync();
        var options = expenseAccounts.Select(a => new SelectListItem(a.Name, a.Id.ToString())).ToList();
        var defaultAccount = expenseAccounts.FirstOrDefault(a => a.Name == "هزینه‌های عمومی");
        var defaultExpenseAccountId = defaultAccount?.Id ?? 0;
        if (defaultAccount is null)
            options.Insert(0, new SelectListItem("هزینه‌های عمومی (حساب پیش‌فرض)", "0"));
        ViewBag.ExpenseAccounts = new SelectList(options, "Value", "Text", (selectedExpenseAccountId ?? defaultExpenseAccountId).ToString());
        return defaultExpenseAccountId;
    }

    private static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = PersianDate.NormalizeDigits(text).Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out amount) && amount > 0;
    }
}
