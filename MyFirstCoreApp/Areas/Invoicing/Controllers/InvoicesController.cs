using DNTPersianUtils.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Invoicing.ViewModels.Invoice;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Invoicing.Controllers;

[Area("Invoicing")]
[Authorize]
public class InvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IInvoiceService invoiceService)
    {
        _context = context;
        _currentTenant = currentTenant;
        _invoiceService = invoiceService;
    }

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
            .Include(x => x.Contact)
            .Where(x => x.TenantId == tenantId.Value);
        
        if (invoiceType.HasValue)
        {
            query = query.Where(x => x.InvoiceType == invoiceType.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
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

        var items = await query
            .Skip(request.StartRow)
            .Take(request.EndRow - request.StartRow)
            .Select(x => new
            {
                id = x.Id,
                number = x.Number,
                date = x.Date.ToString("yyyy-MM-ddTHH:mm:ss"),
                invoiceType = (int)x.InvoiceType,
                invoiceTypeName = x.InvoiceType == InvoiceType.Sale ? "فروش"
                                : x.InvoiceType == InvoiceType.Purchase ? "خرید"
                                : "پیش‌فاکتور",
                contactName = x.Contact != null ? x.Contact.Name : null,
                totalAmount = x.TotalAmount,
                paidAmount = x.PaidAmount,
                remainingAmount = x.TotalAmount - x.PaidAmount,
                status = (int)x.Status,
                // 🆕 statusName با همه‌ی وضعیت‌ها
                statusName = x.Status == InvoiceStatus.Draft ? "پیش‌نویس"
                            : x.Status == InvoiceStatus.Sent ? "ارسال شده"
                            : x.Status == InvoiceStatus.Revised ? "اصلاح شده"
                            : x.Status == InvoiceStatus.Approved ? "تایید شده"
                            : x.Status == InvoiceStatus.Rejected ? "رد شده"
                            : x.Status == InvoiceStatus.Postponed ? "به تعویق"
                            : x.Status == InvoiceStatus.Converted ? "تبدیل شده"
                            : x.Status == InvoiceStatus.Cancelled ? "لغو شده"
                            : x.Status == InvoiceStatus.Posted ? "قطعی"
                            : "نامشخص",
                // 🆕 تاریخ پیگیری
                nextFollowUpDate = x.NextFollowUpDate.HasValue
                    ? x.NextFollowUpDate.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                    : null,
                // 🆕 تاریخچه (فقط تعداد)
                historyCount = x.Histories.Count
            })
            .ToListAsync();

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // GET: /Invoicing/Invoices/Create
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> Create(InvoiceType invoiceType = InvoiceType.Sale)
    {
        var contactOptions = await GetContactOptionsAsync();
        var productOptions = await GetProductOptionsAsync();

        var model = new InvoiceFormViewModel
        {
            Number = await _invoiceService.GetNextNumberAsync(_currentTenant.TenantId ?? 0, invoiceType),
            Date = DateTime.Now.ToShortPersianDateString(),
            InvoiceType = invoiceType,
            ContactOptions = contactOptions,
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

        // 🆕 ۱. اول Contact
        if ((!model.ContactId.HasValue || model.ContactId.Value == 0)
            && !string.IsNullOrWhiteSpace(model.ContactName))
        {
            var contactType = model.InvoiceType == InvoiceType.Purchase
                ? ContactType.Supplier
                : ContactType.Customer;

            var newContact = new Contact
            {
                TenantId = tenantId.Value,
                Name = model.ContactName.Trim(),
                ContactType = contactType,
                IsActive = true
            };

            _context.Contacts.Add(newContact);
            await _context.SaveChangesAsync();

            model.ContactId = newContact.Id;
        }

        // 🆕 ۲. چک ContactId
        if (!model.ContactId.HasValue || model.ContactId.Value == 0)
        {
            ModelState.AddModelError("", "لطفاً طرف حساب را انتخاب کنید.");
        }

        // 🆕 ۳. **اول** validItems رو تعریف کن
        var validItems = model.Items
            .Where(i => i.ProductId > 0 || !string.IsNullOrWhiteSpace(i.ProductName))
            .Where(i => i.Quantity > 0)
            .ToList();

        if (validItems.Count == 0)
        {
            ModelState.AddModelError("", "حداقل یک قلم با کالا و تعداد لازم است.");
        }

        // 🆕 ۴. **بعد** چک تکراری
        // چک تکراری برای کالاهای موجود (ProductId > 0)
        var duplicateProductIds = validItems
            .Where(i => i.ProductId > 0)
            .GroupBy(i => i.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateProductIds.Any())
        {
            ModelState.AddModelError("", "کالاهای تکراری در فاکتور وجود دارد.");
        }

        // 🆕 ۵. چک تکراری برای کالاهای جدید (ProductId == 0)
        var duplicateProductNames = validItems
            .Where(i => i.ProductId == 0 && !string.IsNullOrWhiteSpace(i.ProductName))
            .GroupBy(i => i.ProductName.Trim())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateProductNames.Any())
        {
            ModelState.AddModelError("", "کالاهای جدید تکراری در فاکتور وجود دارد.");
        }

        // 🆕 ۶. حالا ModelState
        if (!ModelState.IsValid)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        // 🆕 ۷. ساخت کالاهای جدید
        for (int i = 0; i < validItems.Count; i++)
        {
            var item = validItems[i];

            if (item.ProductId == 0 && !string.IsNullOrWhiteSpace(item.ProductName))
            {
                var defaultUnit = await _context.Units
                    .FirstOrDefaultAsync(u => u.TenantId == tenantId.Value && u.Name == "عدد");

                if (defaultUnit == null)
                {
                    defaultUnit = await _context.Units
                        .FirstOrDefaultAsync(u => u.TenantId == tenantId.Value && u.IsActive);
                }

                if (defaultUnit == null)
                {
                    defaultUnit = new Unit
                    {
                        TenantId = tenantId.Value,
                        Name = "عدد",
                        Symbol = "عدد",
                        SortOrder = 1,
                        IsActive = true
                    };
                    _context.Units.Add(defaultUnit);
                    await _context.SaveChangesAsync();
                }

                var newProduct = new Product
                {
                    TenantId = tenantId.Value,
                    Name = item.ProductName.Trim(),
                    UnitId = defaultUnit.Id,
                    SalePrice = item.UnitPrice,
                    LastPurchasePrice = 0,
                    IsActive = true
                };

                _context.Products.Add(newProduct);
                await _context.SaveChangesAsync();

                item.ProductId = newProduct.Id;
            }
        }

        // 🆕 ۸. ساخت فاکتور
        var invoice = new Invoice
        {
            TenantId = tenantId.Value,
            Number = model.Number,
            Date = ParsePersianDate(model.Date),
            InvoiceType = model.InvoiceType,
            ContactId = model.ContactId,
            Description = model.Description?.Trim(),
            Items = validItems.Select(i => new InvoiceItem
            {
                TenantId = tenantId.Value,
                ProductId = i.ProductId,
                Description = i.Description?.Trim(),
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountPercent = i.DiscountPercent,
                TaxRate = i.TaxRate
            }).ToList()
        };

        try
        {
            await _invoiceService.CreateAsync(invoice);
            TempData["Success"] = $"فاکتور شماره {invoice.Number} با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)invoice.InvoiceType });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            await ReloadOptionsAsync(model);
            return View(model);
        }
    }

    // ---------- Helpers ----------

    private async Task ReloadOptionsAsync(InvoiceFormViewModel model)
    {
        var productOptions = await GetProductOptionsAsync();
        var contactOptions = await GetContactOptionsAsync();

        model.ContactOptions = contactOptions;
        model.InvoiceTypeOptions = GetInvoiceTypeOptions();

        foreach (var item in model.Items)
        {
            item.ProductOptions = productOptions;
        }
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

    private DateTime ParsePersianDate(string persianDate)
    {
        if (string.IsNullOrWhiteSpace(persianDate))
            return DateTime.UtcNow;

        try
        {
            var parts = persianDate.Split('/', '-');
            if (parts.Length != 3) return DateTime.UtcNow;

            var year = int.Parse(ToEnglishNumber(parts[0]));
            var month = int.Parse(ToEnglishNumber(parts[1]));
            var day = int.Parse(ToEnglishNumber(parts[2]));

            var pc = new System.Globalization.PersianCalendar();
            return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }

    private string ToEnglishNumber(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("۰", "0").Replace("۱", "1").Replace("۲", "2")
            .Replace("۳", "3").Replace("۴", "4").Replace("۵", "5")
            .Replace("۶", "6").Replace("۷", "7").Replace("۸", "8")
            .Replace("۹", "9");
    }

    // POST: /Invoicing/Invoices/Post
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Post")]
    public async Task<IActionResult> Post(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.PostAsync(id, tenantId.Value);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? "";
            var fullMessage = $"{ex.Message} | {innerMsg}";
            return Json(new { success = false, message = fullMessage });
        }
    }

    // POST: /Invoicing/Invoices/Unpost
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Post")]
    public async Task<IActionResult> Unpost(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.UnpostAsync(id, tenantId.Value);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Invoicing/Invoices/Convert
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Create")]
    public async Task<IActionResult> Convert(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            var saleInvoice = await _invoiceService.ConvertToInvoiceAsync(id, tenantId.Value);
            return Json(new { success = true, newId = saleInvoice.Id, message = "پیش‌فاکتور به فاکتور فروش تبدیل شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

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
            q = q.Trim();
            query = query.Where(c => c.Name.Contains(q) || (c.Mobile != null && c.Mobile.Contains(q)));
        }

        var items = await query
            .OrderBy(c => c.Name)
            .Take(50)   // 🆕 بیشتر
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
            .Include(p => p.Unit)
            .Where(p => p.TenantId == tenantId.Value && p.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
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
            .Include(i => i.Histories)   // 🆕
            .Include(i => i.ReferenceInvoice)   // 🆕
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        return View(entity);
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
            .ThenInclude(it => it.Product)   // 🆕 Include Product
            .Include(i => i.Contact)             // 🆕 Include Contact
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (entity.Status == InvoiceStatus.Posted)
        {
            TempData["Error"] = "فاکتور قطعی قابل ویرایش نیست.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)entity.InvoiceType });
        }

        // 🆕 فقط Draft یا Revised قابل ویرایش
        if (entity.Status != InvoiceStatus.Draft && entity.Status != InvoiceStatus.Revised)
        {
            TempData["Error"] = "فقط پیش‌فاکتور پیش‌نویس یا اصلاح‌شده قابل ویرایش است.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)entity.InvoiceType });
        }

        var productOptions = await GetProductOptionsAsync();
        var contactOptions = await GetContactOptionsAsync();

        var model = new InvoiceFormViewModel
        {
            Id = entity.Id,
            Number = entity.Number,
            Date = entity.Date.ToShortPersianDateString(),   // 🆕 شمسی
            InvoiceType = entity.InvoiceType,
            ContactId = entity.ContactId,
            ContactName = entity.Contact?.Name,
            Description = entity.Description,
            ContactOptions = contactOptions,
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

        // 🆕 Contact جدید
        if ((!model.ContactId.HasValue || model.ContactId.Value == 0)
            && !string.IsNullOrWhiteSpace(model.ContactName))
        {
            var contactType = model.InvoiceType == InvoiceType.Purchase
                ? ContactType.Supplier
                : ContactType.Customer;

            var newContact = new Contact
            {
                TenantId = tenantId.Value,
                Name = model.ContactName.Trim(),
                ContactType = contactType,
                IsActive = true
            };
            _context.Contacts.Add(newContact);
            await _context.SaveChangesAsync();
            model.ContactId = newContact.Id;
        }

        // 🆕 چک Contact
        if (!model.ContactId.HasValue || model.ContactId.Value == 0)
        {
            ModelState.AddModelError("", "لطفاً طرف حساب را انتخاب کنید.");
        }

        // 🆕 validItems
        var validItems = model.Items
            .Where(i => (i.ProductId > 0 || !string.IsNullOrWhiteSpace(i.ProductName)) && i.Quantity > 0)
            .ToList();

        if (validItems.Count == 0)
        {
            ModelState.AddModelError("", "حداقل یک قلم با کالا و تعداد لازم است.");
        }

        // 🆕 چک تکراری
        var duplicateProductIds = validItems
            .Where(i => i.ProductId > 0)
            .GroupBy(i => i.ProductId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateProductIds.Any())
            ModelState.AddModelError("", "کالاهای تکراری در فاکتور وجود دارد.");

        var duplicateProductNames = validItems
            .Where(i => i.ProductId == 0 && !string.IsNullOrWhiteSpace(i.ProductName))
            .GroupBy(i => i.ProductName.Trim())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateProductNames.Any())
            ModelState.AddModelError("", "کالاهای جدید تکراری در فاکتور وجود دارد.");

        if (!ModelState.IsValid)
        {
            await ReloadOptionsAsync(model);
            return View(model);
        }

        // 🆕 چک کن قطعی نباشه
        var entity = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (entity.Status == InvoiceStatus.Posted)
        {
            TempData["Error"] = "فاکتور قطعی قابل ویرایش نیست.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)entity.InvoiceType });
        }
        if (entity.Status != InvoiceStatus.Draft && entity.Status != InvoiceStatus.Revised)
        {
            TempData["Error"] = "فقط پیش‌فاکتور پیش‌نویس یا اصلاح‌شده قابل ویرایش است.";
            return RedirectToAction(nameof(Index), new { invoiceType = (int)entity.InvoiceType });
        }

        // 🆕 ساخت کالاهای جدید
        for (int i = 0; i < validItems.Count; i++)
        {
            var item = validItems[i];
            if (item.ProductId == 0 && !string.IsNullOrWhiteSpace(item.ProductName))
            {
                var defaultUnit = await _context.Units
                    .FirstOrDefaultAsync(u => u.TenantId == tenantId.Value && u.Name == "عدد")
                    ?? await _context.Units.FirstOrDefaultAsync(u => u.TenantId == tenantId.Value && u.IsActive);

                if (defaultUnit == null)
                {
                    defaultUnit = new Unit
                    {
                        TenantId = tenantId.Value,
                        Name = "عدد",
                        Symbol = "عدد",
                        SortOrder = 1,
                        IsActive = true
                    };
                    _context.Units.Add(defaultUnit);
                    await _context.SaveChangesAsync();
                }

                var newProduct = new Product
                {
                    TenantId = tenantId.Value,
                    Name = item.ProductName.Trim(),
                    UnitId = defaultUnit.Id,
                    SalePrice = item.UnitPrice,
                    LastPurchasePrice = 0,
                    IsActive = true
                };
                _context.Products.Add(newProduct);
                await _context.SaveChangesAsync();
                item.ProductId = newProduct.Id;
            }
        }

        // 🆕 حذف خطوط قدیمی
        _context.InvoiceItems.RemoveRange(entity.Items);

        // 🆕 به‌روزرسانی
        entity.Date = ParsePersianDate(model.Date);
        entity.InvoiceType = model.InvoiceType;
        entity.ContactId = model.ContactId;
        entity.Description = model.Description?.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        // 🆕 خطوط جدید
        entity.Items = validItems.Select(i => new InvoiceItem
        {
            TenantId = tenantId.Value,
            ProductId = i.ProductId,
            Description = i.Description?.Trim(),
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            DiscountPercent = i.DiscountPercent,
            TaxRate = i.TaxRate
        }).ToList();

        // 🆕 محاسبه جمع‌ها
        decimal subTotal = 0, discountTotal = 0, taxTotal = 0;
        foreach (var it in entity.Items)
        {
            var lineBase = it.Quantity * it.UnitPrice;
            it.DiscountAmount = lineBase * (it.DiscountPercent / 100);
            var afterDiscount = lineBase - it.DiscountAmount;
            it.TaxAmount = afterDiscount * (it.TaxRate / 100);
            it.LineTotal = afterDiscount + it.TaxAmount;

            subTotal += lineBase;
            discountTotal += it.DiscountAmount;
            taxTotal += it.TaxAmount;
        }

        entity.SubTotal = subTotal;
        entity.DiscountAmount = discountTotal;
        entity.TaxAmount = taxTotal;
        entity.TotalAmount = subTotal - discountTotal + taxTotal;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"فاکتور شماره {entity.Number} با موفقیت ویرایش شد.";
        return RedirectToAction(nameof(Index), new { invoiceType = (int)entity.InvoiceType });
    }

    // ============================================
    // Delete
    // ============================================
    
    // POST: /Invoicing/Invoices/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.DeleteAsync(id, tenantId.Value);
            return Json(new { success = true, message = "فاکتور با موفقیت حذف شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ============================================
    // 🆕 گردش کار پیش‌فاکتور
    // ============================================

    // POST: /Invoicing/Invoices/MarkAsSent
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> MarkAsSent(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.MarkAsSentAsync(id, tenantId.Value);
            return Json(new { success = true, message = "پیش‌فاکتور به مشتری ارسال شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Invoicing/Invoices/MarkAsRevised
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> MarkAsRevised(int id, string? description)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.MarkAsRevisedAsync(id, tenantId.Value, description);
            return Json(new { success = true, message = "پیش‌فاکتور اصلاح شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Invoicing/Invoices/MarkAsApproved
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> MarkAsApproved(int id, string? description)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.MarkAsApprovedAsync(id, tenantId.Value, description);
            return Json(new { success = true, message = "پیش‌فاکتور تایید شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Invoicing/Invoices/MarkAsRejected
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> MarkAsRejected(int id, string? reason)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.MarkAsRejectedAsync(id, tenantId.Value, reason);
            return Json(new { success = true, message = "پیش‌فاکتور رد شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Invoicing/Invoices/MarkAsPostponed
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> MarkAsPostponed(int id, string nextFollowUpDate, string? reason)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            // 🆕 تبدیل تاریخ شمسی به میلادی
            var nextDate = ParsePersianDate(nextFollowUpDate);
            if (nextDate == DateTime.MinValue)
            {
                return Json(new { success = false, message = "تاریخ پیگیری نامعتبر است." });
            }

            await _invoiceService.MarkAsPostponedAsync(id, tenantId.Value, nextDate, reason);
            return Json(new { success = true, message = "پیش‌فاکتور به تعویق افتاد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Invoicing/Invoices/MarkAsCancelled
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> MarkAsCancelled(int id, string? reason)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.MarkAsCancelledAsync(id, tenantId.Value, reason);
            return Json(new { success = true, message = "پیش‌فاکتور لغو شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // POST: /Invoicing/Invoices/SettleWithDiscount
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Invoice.Edit")]
    public async Task<IActionResult> SettleWithDiscount(int id, decimal discountAmount)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false, message = "Tenant not found" });

        try
        {
            await _invoiceService.SettleWithDiscountAsync(id, tenantId.Value, discountAmount);
            return Json(new { success = true, message = "تسویه با تخفیف با موفقیت انجام شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}