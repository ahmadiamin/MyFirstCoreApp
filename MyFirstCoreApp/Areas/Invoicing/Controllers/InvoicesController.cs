using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Invoicing;
using MyFirstCoreApp.Areas.Invoicing.ViewModels.Invoice;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Invoicing.Controllers;

[Area("Invoicing")]
[Authorize]
public class InvoicesController : Controller
{
    private const int MaxPageSize = 200;
    private const string InvalidDateMessage = "تاریخ نامعتبر است. نمونه‌ی درست: 1405/01/10";
    private const string NoTenantMessage = "شرکت فعال مشخص نیست.";
    private const string UnexpectedErrorMessage = "خطای غیرمنتظره رخ داد. لطفاً دوباره تلاش کنید.";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IInvoiceService _invoiceService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IInvoiceService invoiceService,
        TimeProvider timeProvider,
        ILogger<InvoicesController> logger)
    {
        _context = context;
        _currentTenant = currentTenant;
        _invoiceService = invoiceService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ============================================
    // Index
    // ============================================

    // GET: /Invoicing/Invoices
    [Authorize(Policy = "Invoice.View")]
    public IActionResult Index(InvoiceType? invoiceType = null)
    {
        ViewBag.InvoiceType = invoiceType;
        return View();
    }

    // POST: /Invoicing/Invoices/GetData
    [HttpPost]
    [Authorize(Policy = "Invoice.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request, [FromQuery] InvoiceType? invoiceType = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Invoices
            .Where(x => x.TenantId == tenantId.Value);

        if (invoiceType.HasValue)
            query = query.Where(x => x.InvoiceType == invoiceType.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = PersianDate.NormalizeDigits(request.Search.Trim());
            query = query.Where(x =>
                x.Number.ToString().Contains(s) ||
                (x.Description != null && x.Description.Contains(s)) ||
                (x.Contact != null && x.Contact.Name.Contains(s)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "number" => request.SortDir == "asc" ? query.OrderBy(x => x.Number) : query.OrderByDescending(x => x.Number),
            "date" => request.SortDir == "asc" ? query.OrderBy(x => x.Date) : query.OrderByDescending(x => x.Date),
            "totalAmount" => request.SortDir == "asc" ? query.OrderBy(x => x.TotalAmount) : query.OrderByDescending(x => x.TotalAmount),
            _ => query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Number)
        };

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
                x.InvoiceType,
                ContactName = x.Contact != null ? x.Contact.Name : null,
                x.TotalAmount,
                x.PaidAmount,
                x.SettlementDiscount,
                x.Status,
                x.NextFollowUpDate,
                HistoryCount = x.Histories.Count
            })
            .ToListAsync();

        // تاریخ شمسی و نام‌ها سمت سرور؛ مانده فقط از InvoiceBalance
        // (قبلاً مانده بدون تخفیف تسویه حساب می‌شد)
        var items = rows.Select(x => new
        {
            id = x.Id,
            number = x.Number,
            date = PersianDate.Format(x.Date),
            invoiceType = (int)x.InvoiceType,
            invoiceTypeName = GetInvoiceTypeName(x.InvoiceType),
            contactName = x.ContactName,
            totalAmount = x.TotalAmount,
            paidAmount = x.PaidAmount,
            remainingAmount = InvoiceBalance.Remaining(x.TotalAmount, x.PaidAmount, x.SettlementDiscount),
            status = (int)x.Status,
            statusName = GetStatusName(x.Status),
            nextFollowUpDate = PersianDate.Format(x.NextFollowUpDate),
            historyCount = x.HistoryCount
        });

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // ============================================
    // Create
    // ============================================

    // GET: /Invoicing/Invoices/Create
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> Create(InvoiceType invoiceType = InvoiceType.Sale)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var productOptions = await GetProductOptionsAsync();

        var model = new InvoiceFormViewModel
        {
            // فقط نمایشی؛ شماره‌ی نهایی هنگام ذخیره تعیین می‌شود
            Number = await _invoiceService.GetNextNumberAsync(tenantId.Value, invoiceType),
            Date = PersianDate.Format(_timeProvider.Today()),
            InvoiceType = invoiceType,
            ContactOptions = await GetContactOptionsAsync(),
            InvoiceTypeOptions = GetInvoiceTypeOptions(),
            Items = new List<InvoiceItemViewModel>
            {
                new() { ProductOptions = productOptions, Quantity = 1 }
            }
        };

        return View(model);
    }

    // POST: /Invoicing/Invoices/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> Create(InvoiceFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

        if (!ModelState.IsValid)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        try
        {
            // طرف‌حساب/کالای جدید داخل همان تراکنش سرویس ساخته می‌شود؛
            // با خطا، هیچ رکورد یتیمی باقی نمی‌ماند.
            var invoice = await _invoiceService.CreateAsync(ToRequest(model, date), tenantId.Value);

            TempData["Success"] = $"فاکتور شماره {invoice.Number} با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)invoice.InvoiceType });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await ReloadOptionsAsync(model);
            return View(model);
        }
    }

    // ============================================
    // Edit
    // ============================================

    // GET: /Invoicing/Invoices/Edit/5
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Invoices
            .Include(i => i.Items)
                .ThenInclude(it => it.Product)
            .Include(i => i.Contact)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (entity.Status == InvoiceStatus.Posted)
        {
            TempData["Error"] = "فاکتور قطعی قابل ویرایش نیست.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)entity.InvoiceType });
        }

        // فقط Draft یا Revised قابل ویرایش
        if (entity.Status != InvoiceStatus.Draft && entity.Status != InvoiceStatus.Revised)
        {
            TempData["Error"] = "فقط فاکتور پیش‌نویس یا پیش‌فاکتور اصلاح‌شده قابل ویرایش است.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)entity.InvoiceType });
        }

        var productOptions = await GetProductOptionsAsync();

        var model = new InvoiceFormViewModel
        {
            Id = entity.Id,
            Number = entity.Number,
            Date = PersianDate.Format(entity.Date),
            InvoiceType = entity.InvoiceType,
            ContactId = entity.ContactId,
            ContactName = entity.Contact?.Name,
            Description = entity.Description,
            ContactOptions = await GetContactOptionsAsync(),
            InvoiceTypeOptions = GetInvoiceTypeOptions(),
            Items = entity.Items.Select(it => new InvoiceItemViewModel
            {
                Id = it.Id,
                ProductId = it.ProductId,
                ProductName = it.Product?.Name,
                Description = it.Description,
                Quantity = it.Quantity,
                UnitPrice = it.UnitPrice,
                DiscountPercent = it.DiscountPercent,
                TaxRate = it.TaxRate,
                ProductOptions = productOptions
            }).ToList()
        };

        return View(model);
    }

    // POST: /Invoicing/Invoices/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> Edit(int id, InvoiceFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

        if (!ModelState.IsValid)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        try
        {
            // سرویس: وضعیت قابل ویرایش، ثابت بودن نوع فاکتور، اعتبارسنجی، محاسبه و ذخیره‌ی اتمیک
            await _invoiceService.UpdateAsync(ToRequest(model, date), tenantId.Value);

            TempData["Success"] = $"فاکتور شماره {model.Number} با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)model.InvoiceType });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            await ReloadOptionsAsync(model);
            return View(model);
        }
    }

    // ============================================
    // Details
    // ============================================

    // GET: /Invoicing/Invoices/Details/5
    [Authorize(Policy = "Invoice.View")]
    public async Task<IActionResult> Details(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Invoices
            .Include(i => i.Contact)
            .Include(i => i.Items)
                .ThenInclude(it => it.Product)
            .Include(i => i.Transaction)
            .Include(i => i.Histories)
            .Include(i => i.ReferenceInvoice)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        return View(entity);
    }

    // ============================================
    // Delete / Post / Unpost / Convert / Settle  (AJAX)
    // ============================================

    // POST: /Invoicing/Invoices/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Delete")]
    public Task<IActionResult> Delete(int id)
        => RunAjaxAsync(tenantId => _invoiceService.DeleteAsync(id, tenantId), "فاکتور با موفقیت حذف شد.");

    // POST: /Invoicing/Invoices/Post
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Post")]
    public Task<IActionResult> Post(int id)
        => RunAjaxAsync(tenantId => _invoiceService.PostAsync(id, tenantId), "فاکتور قطعی شد.");

    // POST: /Invoicing/Invoices/Unpost
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Post")]
    public Task<IActionResult> Unpost(int id)
        => RunAjaxAsync(tenantId => _invoiceService.UnpostAsync(id, tenantId), "فاکتور به پیش‌نویس برگشت.");

    // POST: /Invoicing/Invoices/Convert
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> Convert(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Json(new { success = false, message = NoTenantMessage });

        try
        {
            var saleInvoice = await _invoiceService.ConvertToInvoiceAsync(id, tenantId.Value);
            return Json(new { success = true, newId = saleInvoice.Id, message = "پیش‌فاکتور به فاکتور فروش تبدیل شد." });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice conversion failed");
            return Json(new { success = false, message = UnexpectedErrorMessage });
        }
    }

    // POST: /Invoicing/Invoices/SettleWithDiscount
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> SettleWithDiscount(int id, decimal discountAmount)
        => RunAjaxAsync(
            tenantId => _invoiceService.SettleWithDiscountAsync(id, tenantId, discountAmount),
            "تسویه با تخفیف با موفقیت انجام شد.");

    // ============================================
    // گردش کار پیش‌فاکتور  (AJAX)
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> MarkAsSent(int id)
        => RunAjaxAsync(tenantId => _invoiceService.MarkAsSentAsync(id, tenantId), "پیش‌فاکتور به مشتری ارسال شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> MarkAsRevised(int id, string? description)
        => RunAjaxAsync(tenantId => _invoiceService.MarkAsRevisedAsync(id, tenantId, description), "پیش‌فاکتور اصلاح شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> MarkAsApproved(int id, string? description)
        => RunAjaxAsync(tenantId => _invoiceService.MarkAsApprovedAsync(id, tenantId, description), "پیش‌فاکتور تایید شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> MarkAsRejected(int id, string? reason)
        => RunAjaxAsync(tenantId => _invoiceService.MarkAsRejectedAsync(id, tenantId, reason), "پیش‌فاکتور رد شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> MarkAsPostponed(int id, string nextFollowUpDate, string? reason)
    {
        // تاریخ نامعتبر دیگر بی‌صدا به «امروز» تبدیل نمی‌شود
        if (!PersianDate.TryParse(nextFollowUpDate, out var nextDate))
            return Task.FromResult<IActionResult>(Json(new { success = false, message = "تاریخ پیگیری نامعتبر است." }));

        return RunAjaxAsync(
            tenantId => _invoiceService.MarkAsPostponedAsync(id, tenantId, nextDate, reason),
            "پیش‌فاکتور به تعویق افتاد.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> MarkAsCancelled(int id, string? reason)
        => RunAjaxAsync(tenantId => _invoiceService.MarkAsCancelledAsync(id, tenantId, reason), "پیش‌فاکتور لغو شد.");

    // ============================================
    // AJAX: جستجو و قیمت
    // ============================================

    // GET: /Invoicing/Invoices/GetProductPrice?id=5
    [HttpGet]
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> GetProductPrice(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value);

        if (product == null) return Json(new { success = false });

        return Json(new
        {
            success = true,
            salePrice = product.SalePrice,
            taxRate = product.TaxRate ?? 0
        });
    }

    // GET: /Invoicing/Invoices/SearchContacts?q=علی
    [HttpGet]
    [Authorize(Policy = "Invoice.View")]
    public async Task<IActionResult> SearchContacts(string q = "", int type = 0)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Contacts
            .Where(c => c.TenantId == tenantId.Value && c.IsActive);

        if (type == 1)
            query = query.Where(c => c.ContactType == ContactType.Customer || c.ContactType == ContactType.Both);
        else if (type == 2)
            query = query.Where(c => c.ContactType == ContactType.Supplier || c.ContactType == ContactType.Both);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = PersianDate.NormalizeDigits(q.Trim());
            query = query.Where(c => c.Name.Contains(q) || (c.Mobile != null && c.Mobile.Contains(q)));
        }

        var items = await query
            .OrderBy(c => c.Name)
            .Take(50)
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                mobile = c.Mobile
            })
            .ToListAsync();

        return Json(new { success = true, data = items });
    }

    // GET: /Invoicing/Invoices/SearchProducts?q=لپ
    [HttpGet]
    [Authorize(Policy = "Invoice.View")]
    public async Task<IActionResult> SearchProducts(string q = "")
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Products
            .Where(p => p.TenantId == tenantId.Value && p.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = PersianDate.NormalizeDigits(q.Trim());
            query = query.Where(p =>
                p.Name.Contains(q) ||
                (p.Code != null && p.Code.Contains(q)) ||
                (p.Barcode != null && p.Barcode.Contains(q)));
        }

        var items = await query
            .OrderBy(p => p.Name)
            .Take(50)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                code = p.Code,
                salePrice = p.SalePrice,
                unitName = p.Unit != null ? p.Unit.Name : "عدد",
                taxRate = p.TaxRate ?? 0
            })
            .ToListAsync();

        return Json(new { success = true, data = items });
    }

    // ============================================
    // Helpers
    // ============================================

    /// <summary>
    /// خطای کسب‌وکار (InvalidOperationException) پیام فارسی به کاربر می‌دهد.
    /// هر خطای دیگر لاگ می‌شود و پیام عمومی برمی‌گردد (جزئیات داخلی نشت نمی‌کند).
    /// </summary>
    private async Task<IActionResult> RunAjaxAsync(Func<int, Task> action, string successMessage)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Json(new { success = false, message = NoTenantMessage });

        try
        {
            await action(tenantId.Value);
            return Json(new { success = true, message = successMessage });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice operation failed");
            return Json(new { success = false, message = UnexpectedErrorMessage });
        }
    }

    /// <summary>
    /// فرم → درخواست. ردیف‌های خالی فرم (بدون کالا یا بدون تعداد) نادیده گرفته می‌شوند؛
    /// بقیه‌ی قواعد (تکراری، مقادیر، ...) در InvoiceService اعمال می‌شود.
    /// </summary>
    private static InvoiceRequest ToRequest(InvoiceFormViewModel model, DateOnly date)
    {
        return new InvoiceRequest
        {
            Id = model.Id,
            Date = date,
            InvoiceType = model.InvoiceType,
            ContactId = model.ContactId > 0 ? model.ContactId : null,
            NewContactName = model.ContactName,
            Description = model.Description,
            Items = model.Items
                .Where(i => (i.ProductId > 0 || !string.IsNullOrWhiteSpace(i.ProductName)) && i.Quantity > 0)
                .Select(i => new InvoiceItemRequest
                {
                    ProductId = i.ProductId,
                    NewProductName = i.ProductId > 0 ? null : i.ProductName,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    DiscountPercent = i.DiscountPercent,
                    TaxRate = i.TaxRate
                })
                .ToList()
        };
    }

    private async Task ReloadOptionsAsync(InvoiceFormViewModel model)
    {
        var productOptions = await GetProductOptionsAsync();

        model.ContactOptions = await GetContactOptionsAsync();
        model.InvoiceTypeOptions = GetInvoiceTypeOptions();

        foreach (var item in model.Items)
            item.ProductOptions = productOptions;
    }

    private async Task<List<SelectListItem>> GetProductOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Products
            .Where(p => p.TenantId == tenantId.Value && p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name + (p.Code != null ? $" ({p.Code})" : "")
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetContactOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Contacts
            .Where(c => c.TenantId == tenantId.Value && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToListAsync();
    }

    private static List<SelectListItem> GetInvoiceTypeOptions()
    {
        return new List<SelectListItem>
        {
            new() { Value = "1", Text = "پیش‌فاکتور" },
            new() { Value = "2", Text = "فروش" },
            new() { Value = "3", Text = "خرید" }
        };
    }

    private static string GetInvoiceTypeName(InvoiceType type) => type switch
    {
        InvoiceType.Sale => "فروش",
        InvoiceType.Purchase => "خرید",
        _ => "پیش‌فاکتور"
    };

    private static string GetStatusName(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "پیش‌نویس",
        InvoiceStatus.Sent => "ارسال شده",
        InvoiceStatus.Revised => "اصلاح شده",
        InvoiceStatus.Approved => "تایید شده",
        InvoiceStatus.Rejected => "رد شده",
        InvoiceStatus.Postponed => "به تعویق",
        InvoiceStatus.Converted => "تبدیل شده",
        InvoiceStatus.Cancelled => "لغو شده",
        InvoiceStatus.Posted => "قطعی",
        _ => "نامشخص"
    };
}
