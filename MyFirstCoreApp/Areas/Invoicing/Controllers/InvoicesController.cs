using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Invoicing;
using MyFirstCoreApp.Areas.Invoicing.ViewModels.Invoice;
using MyFirstCoreApp.Areas.Invoicing.ViewModels.InvoiceReturn;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using Stimulsoft.Report;
using System.Data;

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
    private readonly IStockBalanceService _stockBalanceService;

    public InvoicesController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IInvoiceService invoiceService,
        TimeProvider timeProvider,
        ILogger<InvoicesController> logger,
        IStockBalanceService stockBalanceService)
    {
        _context = context;
        _currentTenant = currentTenant;
        _invoiceService = invoiceService;
        _timeProvider = timeProvider;
        _logger = logger;
        _stockBalanceService = stockBalanceService;
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
                x.DueDate,
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
        var today = _timeProvider.Today(); 
        
        var items = rows.Select(x => new
        {
            id = x.Id,
            number = x.Number,
            date = PersianDate.Format(x.Date),
            dueDate = PersianDate.Format(x.DueDate),
            // معوق = قطعی + دارای باقی‌مانده + سررسید قبل از امروز (بدون سررسید هیچ‌وقت معوق نیست)
            isOverdue = x.Status == InvoiceStatus.Posted
                        && x.DueDate.HasValue
                        && x.DueDate.Value < today
                        && InvoiceBalance.Remaining(x.TotalAmount, x.PaidAmount, x.SettlementDiscount) > 0,
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
            WarehouseOptions = await GetWarehouseOptionsAsync(tenantId.Value),
            Items = new List<InvoiceItemViewModel>
            {
                new() { ProductOptions = productOptions, Quantity = 1 }
            }
        };
        model.DefaultWarehouseId = GetDefaultWarehouseId(model.WarehouseOptions);

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

        var dueDate = ParseDueDate(model);

        if (!ModelState.IsValid)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        try
        {
            // طرف‌حساب/کالای جدید داخل همان تراکنش سرویس ساخته می‌شود؛
            // با خطا، هیچ رکورد یتیمی باقی نمی‌ماند.
            var invoice = await _invoiceService.CreateAsync(ToRequest(model, date, dueDate), tenantId.Value);

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
            DueDate = entity.DueDate.HasValue ? PersianDate.Format(entity.DueDate.Value) : null,
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
                WarehouseId = it.WarehouseId,
                TrackInventory = it.Product?.TrackInventory,
                ProductName = it.Product?.Name,
                Description = it.Description,
                Quantity = it.Quantity,
                UnitPrice = it.UnitPrice,
                DiscountPercent = it.DiscountPercent,
                TaxRate = it.TaxRate,
                ProductOptions = productOptions
            }).ToList(),
            WarehouseOptions = await GetWarehouseOptionsAsync(tenantId.Value)
        };
        model.DefaultWarehouseId = GetDefaultWarehouseId(model.WarehouseOptions);

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
        
        var dueDate = ParseDueDate(model);

        if (!ModelState.IsValid)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        try
        {
            // سرویس: وضعیت قابل ویرایش، ثابت بودن نوع فاکتور، اعتبارسنجی، محاسبه و ذخیره‌ی اتمیک
            await _invoiceService.UpdateAsync(ToRequest(model, date, dueDate), tenantId.Value);

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
            .Include(i => i.Items)
                .ThenInclude(it => it.Warehouse)
            .Include(i => i.Transaction)
            .Include(i => i.Histories)
            .Include(i => i.ReferenceInvoice)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        // فاکتورهای برگشتی مرتبط با فاکتور اصلی (محدود به همان مستأجر)
        ViewBag.ReturnInvoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId.Value && i.ReferenceInvoiceId == entity.Id)
            .OrderByDescending(i => i.Date)
            .ThenByDescending(i => i.Number)
            .ToListAsync();

        return View(entity);
    }

    // ============================================
    // برگشت از فروش / برگشت از خرید
    // ============================================

    // GET: /Invoicing/Invoices/Return/5
    [HttpGet]
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> Return(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        try
        {
            var model = await BuildReturnViewModelAsync(
                id,
                PersianDate.Format(_timeProvider.Today()),
                null,
                null,
                tenantId.Value);

            if (model.Items.Count == 0)
            {
                TempData["Error"] = "تمام اقلام این فاکتور قبلاً برگشت داده شده‌اند.";
                return RedirectToAction(nameof(Details), new { id });
            }

            return View(model);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
    // POST: /Invoicing/Invoices/Return
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> Return(InvoiceReturnViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

        var requestedItems = model.Items?
            .Where(x => x.ReturnQuantity > 0)
            .Select(x => new InvoiceReturnItemRequest
            {
                OriginalInvoiceItemId = x.OriginalInvoiceItemId,
                Quantity = x.ReturnQuantity
            })
            .ToList() ?? new List<InvoiceReturnItemRequest>();

        if (requestedItems.Count == 0)
            ModelState.AddModelError(nameof(model.Items), "برای حداقل یک قلم، مقدار برگشتی وارد کنید.");

        if (!ModelState.IsValid)
        {
            model = await BuildReturnViewModelAsync(
                model.OriginalInvoiceId,
                model.Date,
                model.Description,
                model.Items,
                tenantId.Value);
            return View(model);
        }

        try
        {
            var invoice = await _invoiceService.CreateReturnAsync(
                new InvoiceReturnRequest
                {
                    OriginalInvoiceId = model.OriginalInvoiceId,
                    Date = date,
                    Description = model.Description,
                    Items = requestedItems
                },
                tenantId.Value);

            TempData["Success"] = $"پیش‌نویس برگشت شماره {invoice.Number} ایجاد شد.";
            return RedirectToAction(nameof(Details), new { id = invoice.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            model = await BuildReturnViewModelAsync(
                model.OriginalInvoiceId,
                model.Date,
                model.Description,
                model.Items,
                tenantId.Value);
            return View(model);
        }
    }

    private async Task<InvoiceReturnViewModel> BuildReturnViewModelAsync(
        int originalInvoiceId,
        string? date,
        string? description,
        IEnumerable<InvoiceReturnItemViewModel>? submittedItems,
        int tenantId)
    {
        var original = await _context.Invoices
            .AsNoTracking()
            .Include(x => x.Contact)
            .FirstOrDefaultAsync(x =>
                x.Id == originalInvoiceId &&
                x.TenantId == tenantId)
            ?? throw new InvalidOperationException("فاکتور اصلی پیدا نشد.");

        var returnableItems = await _invoiceService.GetReturnableItemsAsync(
            originalInvoiceId,
            tenantId);

        var submittedQuantities = submittedItems?
            .GroupBy(x => x.OriginalInvoiceItemId)
            .ToDictionary(g => g.Key, g => g.First().ReturnQuantity)
            ?? new Dictionary<int, decimal>();

        return new InvoiceReturnViewModel
        {
            OriginalInvoiceId = original.Id,
            OriginalInvoiceNumber = original.Number,
            ContactName = original.Contact?.Name ?? "",
            ReturnTypeName = original.InvoiceType == InvoiceType.Sale
                ? "برگشت از فروش"
                : "برگشت از خرید",
            Date = string.IsNullOrWhiteSpace(date)
                ? PersianDate.Format(_timeProvider.Today())
                : date,
            Description = description,
            Items = returnableItems.Select(x => new InvoiceReturnItemViewModel
            {
                OriginalInvoiceItemId = x.OriginalInvoiceItemId,
                ProductName = x.ProductName,
                OriginalQuantity = x.OriginalQuantity,
                ReturnedQuantity = x.ReturnedQuantity,
                RemainingQuantity = x.RemainingQuantity,
                ReturnQuantity = submittedQuantities.GetValueOrDefault(x.OriginalInvoiceItemId)
            }).ToList()
        };
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
    // تمدید سررسید  (AJAX)
    // ============================================

    // POST: /Invoicing/Invoices/ExtendDueDate
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public Task<IActionResult> ExtendDueDate(int id, string newDueDate, string? reason)
    {
        if (!PersianDate.TryParse(newDueDate, out var due))
            return Task.FromResult<IActionResult>(Json(new { success = false, message = InvalidDateMessage }));

        return RunAjaxAsync(
            tenantId => _invoiceService.ExtendDueDateAsync(id, tenantId, due, reason),
            "سررسید فاکتور تغییر کرد.");
    }

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
    public async Task<IActionResult> SearchProducts(
        string q = "",
        InvoiceType? invoiceType = null,
        int? warehouseId = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Json(new { success = false });

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
                salePrice = invoiceType == InvoiceType.Purchase
                    ? p.LastPurchasePrice
                    : p.SalePrice,
                unitName = p.Unit != null ? p.Unit.Name : "عدد",
                taxRate = p.TaxRate ?? 0,
                trackInventory = p.TrackInventory
            })
            .ToListAsync();

        var behaviorSetting = await _context.InvoiceBehaviorSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value);

        var showStock = behaviorSetting?.ShowStockInInvoice ?? true;

        Dictionary<int, decimal> stockBalances = new();
        if (showStock)
        {
            // اگر انبار مشخص نشده، انبار پیش‌فرض شرکت را پیدا کن.
            if (!warehouseId.HasValue || warehouseId.Value <= 0)
            {
                var warehouseOptions = await GetWarehouseOptionsAsync(tenantId.Value);
                warehouseId = GetDefaultWarehouseId(warehouseOptions);
            }

            if (warehouseId.HasValue)
            {
                var balances = await _stockBalanceService.GetWarehouseBalancesAsync(
                    warehouseId.Value,
                    tenantId.Value);

                stockBalances = balances.ToDictionary(
                    x => x.ProductId,
                    x => x.Quantity);
            }
        }

        var data = items.Select(p => new
        {
            p.id,
            p.name,
            p.code,
            p.salePrice,
            p.unitName,
            p.taxRate,
            p.trackInventory,
            stock = showStock && p.trackInventory
                ? stockBalances.GetValueOrDefault(p.id, 0)
                : (decimal?)null
        });

        return Json(new
        {
            success = true,
            showStock,
            data
        });
    }

    // GET: /Invoicing/Invoices/Print/5?showBalance=true
    [Authorize(Policy = "Invoice.View")]
    public async Task<IActionResult> Print(
        int id,
        bool showBalance = true)
    {
        var tenantId = _currentTenant.TenantId;

        if (!tenantId.HasValue)
            return Forbid();

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Contact)
            .Include(i => i.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.TenantId == tenantId.Value);

        if (invoice == null)
            return NotFound();

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.Id == tenantId.Value);

        if (tenant == null)
            return NotFound();

        decimal contactBalance = 0;

        if (showBalance && invoice.ContactId.HasValue)
        {
            var balanceQuery = _context.TransactionLines
                .AsNoTracking()
                .Where(l =>
                    l.TenantId == tenantId.Value &&
                    l.ContactId == invoice.ContactId.Value &&
                    !l.IsDeleted &&
                    !l.Transaction.IsDeleted &&
                    l.Transaction.IsPosted &&
                    l.Transaction.Date <= invoice.Date);

            var debit = await balanceQuery
                .SumAsync(l => (decimal?)l.Debit) ?? 0;

            var credit = await balanceQuery
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            contactBalance = debit - credit;
        }

        // در مرحله بعد، اطلاعات بالا به قالب Invoice.mrt
        // ارسال و خروجی PDF تولید می‌شود.
        return Ok(new
        {
            InvoiceId = invoice.Id,
            invoice.Number,
            invoice.Date,
            invoice.InvoiceType,
            CompanyName = tenant.LegalName ?? tenant.Name,
            ContactName = invoice.Contact?.Name,
            ItemCount = invoice.Items.Count,
            ShowBalance = showBalance,
            ContactBalance = contactBalance
        });
    }

    [HttpGet]
    [Authorize(Policy = "Invoice.View")]
    public async Task<IActionResult> PrintPdf(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Contact)
            .Include(i => i.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(i =>
                i.Id == id &&
                i.TenantId == tenantId.Value);

        if (invoice == null)
            return NotFound();

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value);

        if (tenant == null)
            return NotFound();

        // اطلاعات ردیف‌های فاکتور
        var itemsTable = new DataTable("Invoices");
        itemsTable.Columns.Add("ProductName", typeof(string));
        itemsTable.Columns.Add("Quantity", typeof(decimal));
        itemsTable.Columns.Add("UnitPrice", typeof(decimal));
        itemsTable.Columns.Add("LineTotal", typeof(decimal));

        foreach (var item in invoice.Items)
        {
            itemsTable.Rows.Add(
                item.Description ?? item.Product?.Name ?? "",
                item.Quantity,
                item.UnitPrice,
                item.LineTotal);
        }

        var reportPath = Path.Combine(
            AppContext.BaseDirectory,
            "Reports",
            "Invoice.mrt");

        if (!System.IO.File.Exists(reportPath))
            return Problem("فایل قالب Invoice.mrt پیدا نشد.");

        using var report = new StiReport();
        report.Load(reportPath);

        // اتصال اطلاعات ردیف‌ها به Business Object قالب
        report.RegBusinessObject("Invoices", itemsTable);

        // فعلاً این خط را حذف کن؛ ساختار ستون‌ها از قبل در MRT تعریف شده است.
        // report.Dictionary.SynchronizeBusinessObjects(3);

        report.Compile();

        // اطلاعات شرکت و فاکتور
        report["CompanyName"] = tenant.LegalName ?? tenant.Name ?? "";
        report["CompanyAddress"] = tenant.Address ?? "";
        report["CompanyPhone"] = tenant.Phone ?? "";

        report["InvoiceTitle"] = invoice.InvoiceType switch
        {
            InvoiceType.PreInvoice => "پیش‌فاکتور",
            InvoiceType.Sale => "فاکتور فروش",
            InvoiceType.Purchase => "فاکتور خرید",
            InvoiceType.SalesReturn => "برگشت از فروش",
            InvoiceType.PurchaseReturn => "برگشت از خرید",
            _ => "فاکتور"
        };

        report["InvoiceNumber"] = invoice.Number.ToString();
        report["InvoiceDate"] = PersianDate.Format(invoice.Date);

        report["ContactName"] = invoice.Contact?.Name ?? "";       
        report["ContactAddress"] = string.IsNullOrWhiteSpace(invoice.Contact?.Address)
            ? ""
            : $"آدرس: {invoice.Contact.Address}";
        report["ContactPhone"] = invoice.Contact?.Mobile ?? invoice.Contact?.Phone ?? "";

        report["TotalAmount"] = invoice.TotalAmount;
        var printSettings = await _context.InvoicePrintSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                !x.IsDeleted);

        var showBalance = printSettings?.ShowBalance ?? true;

        var poemMode = printSettings?.PoemSelectionMode
            ?? InvoicePoemSelectionMode.Random;

        string poem = string.Empty;

        if (poemMode == InvoicePoemSelectionMode.Selected &&
            printSettings?.SelectedInvoicePoemId is int selectedPoemId)
        {
            poem = await _context.InvoicePoems
                .AsNoTracking()
                .Where(x =>
                    x.Id == selectedPoemId &&
                    x.TenantId == tenantId.Value &&
                    x.IsActive &&
                    !x.IsDeleted)
                .Select(x => x.Text)
                .FirstOrDefaultAsync() ?? string.Empty;
        }
        else if (poemMode == InvoicePoemSelectionMode.Random)
        {
            var poems = await _context.InvoicePoems
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId.Value &&
                    x.IsActive &&
                    !x.IsDeleted)
                .Select(x => x.Text)
                .ToListAsync();

            if (poems.Count > 0)
                poem = poems[Random.Shared.Next(poems.Count)];
        }

        decimal contactBalance = 0;
        if (showBalance && invoice.ContactId.HasValue)
        {
            var balanceQuery = _context.TransactionLines
                .AsNoTracking()
                .Where(l =>
                    l.TenantId == tenantId.Value &&
                    l.ContactId == invoice.ContactId.Value &&
                    !l.IsDeleted &&
                    !l.Transaction.IsDeleted &&
                    l.Transaction.IsPosted &&
                    l.Transaction.Date <= invoice.Date);

            var debit = await balanceQuery
                .SumAsync(l => (decimal?)l.Debit) ?? 0;

            var credit = await balanceQuery
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            contactBalance = debit - credit;
        }

        var balanceTitle = contactBalance switch
        {
            > 0 => "مانده بدهی ",
            < 0 => "مانده بستانکاری ",
            _ => "حساب تسویه است"
        };
        
        report["ContactBalanceTitle"] = balanceTitle;
        report["AmountInWords"] = NumToChar.NumToStr(invoice.TotalAmount.ToString()) + " ریال";
        report["Description"] = invoice.InvoiceType switch
        {
            InvoiceType.PreInvoice => printSettings?.Description,
            InvoiceType.Sale => printSettings?.Description,
            InvoiceType.Purchase => "",
            _ => ""
        };
        report["ACouplet"] = poem;
        report["ShowBalance"] = showBalance;

        report["BuyerOrSeller"] = invoice.InvoiceType == InvoiceType.Purchase ? "فروشنده" : "خریدار";
        report["ContactBalance"] = contactBalance;

        report.Render(false);

        using var stream = new MemoryStream();
        report.ExportDocument(StiExportFormat.Pdf, stream);

        return File(
            stream.ToArray(),
            "application/pdf",
            $"Invoice-{invoice.Number}.pdf");
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
    /// سررسید اختیاری است: خالی = بدون سررسید؛ پر ولی نامعتبر = خطای فیلد
    /// (قبلاً تاریخ نامعتبر بی‌صدا نادیده گرفته می‌شد).
    /// </summary>
    private DateOnly? ParseDueDate(InvoiceFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DueDate))
            return null;

        if (PersianDate.TryParse(model.DueDate, out var due))
            return due;

        ModelState.AddModelError(nameof(model.DueDate), InvalidDateMessage);
        return null;
    }

    /// <summary>
    /// فرم → درخواست. ردیف‌های خالی فرم (بدون کالا یا بدون تعداد) نادیده گرفته می‌شوند؛
    /// بقیه‌ی قواعد (تکراری، مقادیر، ...) در InvoiceService اعمال می‌شود.
    /// </summary>
    private static InvoiceRequest ToRequest(InvoiceFormViewModel model, DateOnly date, DateOnly? dueDate)
    {
        return new InvoiceRequest
        {
            Id = model.Id,
            Date = date,
            DueDate = dueDate,
            InvoiceType = model.InvoiceType,
            ContactId = model.ContactId > 0 ? model.ContactId : null,
            NewContactName = model.ContactName,
            Description = model.Description,
            Items = model.Items
                .Where(i => (i.ProductId > 0 || !string.IsNullOrWhiteSpace(i.ProductName)) && i.Quantity > 0)
                .Select(i => new InvoiceItemRequest
                {
                    ProductId = i.ProductId,
                    WarehouseId = i.WarehouseId,
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
        var tenantId = _currentTenant.TenantId;
        model.WarehouseOptions = tenantId.HasValue
            ? await GetWarehouseOptionsAsync(tenantId.Value)
            : new();
        model.DefaultWarehouseId = GetDefaultWarehouseId(model.WarehouseOptions);

        foreach (var item in model.Items)
        {
            item.ProductOptions = productOptions;
        }
    }

    private async Task<List<SelectListItem>> GetWarehouseOptionsAsync(int tenantId)
    {
        var warehouses = await _context.Warehouses
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .Select(w => new { w.Id, w.Name, w.IsDefault })
            .ToListAsync();

        return warehouses.Select(w => new SelectListItem
        {
            Value = w.Id.ToString(),
            Text = w.Name + (w.IsDefault ? " (پیش‌فرض)" : ""),
            Selected = w.IsDefault
        }).ToList();
    }

    private static int? GetDefaultWarehouseId(List<SelectListItem> options)
        => int.TryParse(options.FirstOrDefault(x => x.Selected)?.Value, out var id)
            ? id
            : null;

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
        InvoiceType.SalesReturn => "برگشت از فروش",
        InvoiceType.PurchaseReturn => "برگشت از خرید",
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