using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.Banking;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Models;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Banking.ViewModels.Payment;
using MyFirstCoreApp.Domain.Entities.Banking;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;

namespace MyFirstCoreApp.Areas.Banking.Controllers;

[Area("Banking")]
[Authorize]
public class PaymentsController : Controller
{
    private const int MaxPageSize = 200;
    private const string InvalidDateMessage = "تاریخ نامعتبر است. نمونه‌ی درست: 1405/01/10";
    private const string NoTenantMessage = "شرکت فعال مشخص نیست.";
    private const string UnexpectedErrorMessage = "خطای غیرمنتظره رخ داد. لطفاً دوباره تلاش کنید.";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IPaymentService _paymentService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IPaymentService paymentService,
        TimeProvider timeProvider,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _currentTenant = currentTenant;
        _paymentService = paymentService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // ============================================
    // Index
    // ============================================

    [Authorize(Policy = "Payment.View")]
    public IActionResult Index(PaymentType? paymentType = null)
    {
        ViewBag.PaymentType = paymentType;
        return View();
    }

    [HttpPost]
    [Authorize(Policy = "Payment.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request, [FromQuery] PaymentType? paymentType = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var query = _context.Payments
            .Where(x => x.TenantId == tenantId.Value);

        if (paymentType.HasValue)
            query = query.Where(x => x.PaymentType == paymentType.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = PersianDate.NormalizeDigits(request.Search.Trim());
            query = query.Where(x =>
                x.Number.ToString().Contains(s) ||
                (x.Reference != null && x.Reference.Contains(s)) ||
                (x.Description != null && x.Description.Contains(s)) ||
                (x.Contact != null && x.Contact.Name.Contains(s)));
        }

        var total = await query.CountAsync();

        query = request.SortBy switch
        {
            "number" => request.SortDir == "asc" ? query.OrderBy(x => x.Number) : query.OrderByDescending(x => x.Number),
            "date" => request.SortDir == "asc" ? query.OrderBy(x => x.Date) : query.OrderByDescending(x => x.Date),
            "amount" => request.SortDir == "asc" ? query.OrderBy(x => x.Amount) : query.OrderByDescending(x => x.Amount),
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
                x.PaymentType,
                ContactName = x.Contact != null ? x.Contact.Name : null,
                BankAccountName = x.BankAccount != null ? x.BankAccount.Name : null,
                x.Amount,
                x.AllocatedAmount,
                x.Reference,
                x.Status
            })
            .ToListAsync();

        // تاریخ شمسی سمت سرور (DateOnly.ToString با قالب زمان‌دار خطا می‌دهد)
        var items = rows.Select(x => new
        {
            id = x.Id,
            number = x.Number,
            date = PersianDate.Format(x.Date),
            paymentType = (int)x.PaymentType,
            paymentTypeName = x.PaymentType == PaymentType.Received ? "دریافت" : "پرداخت",
            contactName = x.ContactName,
            bankAccountName = x.BankAccountName,
            amount = x.Amount,
            allocatedAmount = x.AllocatedAmount,
            unallocatedAmount = x.Amount - x.AllocatedAmount,
            reference = x.Reference,
            status = (int)x.Status,
            statusName = x.Status == PaymentStatus.Draft ? "پیش‌نویس"
                       : x.Status == PaymentStatus.Posted ? "قطعی"
                       : "لغو شده"
        });

        return Json(new { success = true, rows = items, lastRow = total });
    }

    // ============================================
    // Create
    // ============================================

    [Authorize(Policy = "Payment.Create")]
    public async Task<IActionResult> Create(PaymentType paymentType = PaymentType.Received)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var model = new PaymentFormViewModel
        {
            // فقط نمایشی؛ شماره‌ی نهایی هنگام ذخیره تعیین می‌شود
            Number = await _paymentService.GetNextNumberAsync(tenantId.Value),
            PaymentType = paymentType,
            Date = PersianDate.Format(_timeProvider.Today()),
            ContactOptions = await GetContactOptionsAsync(paymentType),
            BankAccountOptions = await GetBankAccountOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Payment.Create")]
    public async Task<IActionResult> Create(PaymentFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

        if (!ModelState.IsValid)
            return await ReturnFormAsync(model, tenantId.Value);

        var payment = new Payment
        {
            TenantId = tenantId.Value,
            Number = 0, // خودکار
            PaymentType = model.PaymentType,
            Date = date,
            ContactId = model.ContactId,
            BankAccountId = model.BankAccountId,
            ChequeId = model.ChequeId,
            PaymentMethod = model.PaymentMethod,
            Amount = model.Amount,
            Reference = model.Reference?.Trim(),
            Description = model.Description?.Trim(),
            Allocations = BuildAllocations(model, tenantId.Value)
        };

        try
        {
            await _paymentService.CreateAsync(payment);
            TempData["Success"] = $"پرداخت شماره {payment.Number} با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index), new { paymentType = (int)payment.PaymentType });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await ReturnFormAsync(model, tenantId.Value);
        }
    }

    // ============================================
    // Edit
    // ============================================

    [Authorize(Policy = "Payment.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Payments
            .Include(p => p.Allocations)
                .ThenInclude(a => a.Invoice)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        if (entity.Status != PaymentStatus.Draft)
        {
            TempData["Error"] = "فقط پرداخت پیش‌نویس قابل ویرایش است.";
            return RedirectToAction(nameof(Index));
        }

        var model = new PaymentFormViewModel
        {
            Id = entity.Id,
            Number = entity.Number,
            PaymentType = entity.PaymentType,
            Date = PersianDate.Format(entity.Date),
            ContactId = entity.ContactId,
            BankAccountId = entity.BankAccountId,
            ChequeId = entity.ChequeId,
            PaymentMethod = entity.PaymentMethod,
            Amount = entity.Amount,
            Reference = entity.Reference,
            Description = entity.Description,
            ImagePath = entity.ImagePath,
            ContactOptions = await GetContactOptionsAsync(entity.PaymentType),
            BankAccountOptions = await GetBankAccountOptionsAsync(),
            Allocations = entity.Allocations.Select(a => new PaymentAllocationViewModel
            {
                Id = a.Id,
                InvoiceId = a.InvoiceId,
                InvoiceNumber = a.Invoice.Number,
                InvoiceTotal = a.Invoice.TotalAmount,
                InvoicePaid = a.Invoice.PaidAmount,
                InvoiceRemaining = InvoiceBalance.Remaining(a.Invoice),
                InvoiceDate = FormatInvoiceDate(a.Invoice.Date),
                Amount = a.Amount,
                Description = a.Description
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Payment.Edit")]
    public async Task<IActionResult> Edit(int id, PaymentFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (id != model.Id) return BadRequest();

        if (!PersianDate.TryParse(model.Date, out var date))
            ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

        if (!ModelState.IsValid)
            return await ReturnFormAsync(model, tenantId.Value);

        var payment = new Payment
        {
            Id = model.Id,
            TenantId = tenantId.Value,
            PaymentType = model.PaymentType,
            Date = date,
            ContactId = model.ContactId,
            BankAccountId = model.BankAccountId,
            ChequeId = model.ChequeId,
            PaymentMethod = model.PaymentMethod,
            Amount = model.Amount,
            Reference = model.Reference?.Trim(),
            Description = model.Description?.Trim(),
            ImagePath = model.ImagePath,
            Allocations = BuildAllocations(model, tenantId.Value)
        };

        try
        {
            await _paymentService.UpdateAsync(payment);
            TempData["Success"] = "پرداخت با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Index), new { paymentType = (int)payment.PaymentType });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return await ReturnFormAsync(model, tenantId.Value);
        }
    }

    // ============================================
    // Details
    // ============================================

    [Authorize(Policy = "Payment.View")]
    public async Task<IActionResult> Details(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var entity = await _context.Payments
            .Include(p => p.Contact)
            .Include(p => p.BankAccount)
            .Include(p => p.Transaction)
            .Include(p => p.Histories)
            .Include(p => p.Allocations)
                .ThenInclude(a => a.Invoice)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId.Value);

        if (entity == null) return NotFound();

        return View(entity);
    }

    // ============================================
    // Delete / Post / Unpost  (AJAX)
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Payment.Delete")]
    public Task<IActionResult> Delete(int id)
        => RunAjaxAsync(tenantId => _paymentService.DeleteAsync(id, tenantId), "پرداخت با موفقیت حذف شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Payment.Post")]
    public Task<IActionResult> Post(int id)
        => RunAjaxAsync(tenantId => _paymentService.PostAsync(id, tenantId), "پرداخت قطعی شد.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Payment.Post")]
    public Task<IActionResult> Unpost(int id)
        => RunAjaxAsync(tenantId => _paymentService.UnpostAsync(id, tenantId), "پرداخت به پیش‌نویس برگشت.");

    // ============================================
    // AJAX: فاکتورهای باز طرف حساب
    // ============================================

    [HttpGet]
    [Authorize(Policy = "Payment.View")]
    public async Task<IActionResult> GetInvoices(int contactId, PaymentType paymentType)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var invoices = await QueryOpenInvoices(tenantId.Value, contactId, paymentType)
            .Select(i => new { i.Id, i.Number, i.Date, i.TotalAmount, i.PaidAmount, i.SettlementDiscount })
            .ToListAsync();

        var data = invoices.Select(i => new
        {
            id = i.Id,
            number = i.Number,
            dateShamsi = FormatInvoiceDate(i.Date),
            total = i.TotalAmount,
            paid = i.PaidAmount,
            remaining = InvoiceBalance.Remaining(i.TotalAmount, i.PaidAmount, i.SettlementDiscount)
        });

        return Json(new { success = true, data });
    }

    // ============================================
    // AJAX: چک‌های قابل استفاده در پرداخت (روش چک)
    // ============================================

    /// <summary>
    /// چک‌های هم‌جهت و هم‌طرف‌حسابی که هنوز در پرداخت دیگری استفاده نشده‌اند.
    /// paymentId: هنگام ویرایش، چکِ خودِ همین پرداخت هم در لیست بماند.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Payment.View")]
    public async Task<IActionResult> GetCheques(int contactId, PaymentType paymentType, int paymentId = 0)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Json(new { success = false });

        var direction = paymentType == PaymentType.Received ? ChequeDirection.Received : ChequeDirection.Issued;

        var usedChequeIds = _context.Payments
            .Where(p => p.ChequeId != null && p.Id != paymentId && p.Status != PaymentStatus.Cancelled)
            .Select(p => p.ChequeId!.Value);

        var rows = await _context.Cheques
            .Where(c => c.TenantId == tenantId.Value
                     && c.ContactId == contactId
                     && c.Direction == direction
                     && !usedChequeIds.Contains(c.Id))
            .OrderBy(c => c.DueDate)
            .Select(c => new { c.Id, c.ChequeNumber, c.Amount, c.DueDate, c.Status, c.BankName })
            .ToListAsync();

        // وضعیت مجاز در حافظه بررسی می‌شود تا قاعده فقط در ChequeRules تعریف شود
        var data = rows
            .Where(c => ChequeRules.CanBePaymentSource(c.Status))
            .Select(c => new
            {
                id = c.Id,
                chequeNumber = c.ChequeNumber,
                amount = c.Amount,
                bankName = c.BankName,
                dueDateShamsi = PersianDate.Format(c.DueDate)
            });

        return Json(new { success = true, data });
    }

    // ============================================
    // Helpers
    // ============================================

    /// <summary>
    /// خطای کسب‌وکار (InvalidOperationException) پیام فارسی به کاربر می‌دهد.
    /// هر خطای دیگر لاگ می‌شود و پیام عمومی برمی‌گردد (جزئیات داخلی به کاربر نشان داده نمی‌شود).
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
            _logger.LogError(ex, "Payment operation failed");
            return Json(new { success = false, message = UnexpectedErrorMessage });
        }
    }

    private static List<PaymentAllocation> BuildAllocations(PaymentFormViewModel model, int tenantId)
    {
        return model.Allocations
            .Where(a => a.InvoiceId > 0 && a.Amount > 0)
            .Select(a => new PaymentAllocation
            {
                TenantId = tenantId,
                InvoiceId = a.InvoiceId,
                Amount = a.Amount,
                Description = a.Description?.Trim()
            })
            .ToList();
    }

    /// <summary>
    /// فرم را با Optionها و جدول فاکتورهای باز پر می‌کند (مبلغ‌های واردشده‌ی کاربر حفظ می‌شود)
    /// و View همین Action (Create یا Edit) را برمی‌گرداند.
    /// </summary>
    private async Task<IActionResult> ReturnFormAsync(PaymentFormViewModel model, int tenantId)
    {
        model.ContactOptions = await GetContactOptionsAsync(model.PaymentType);
        model.BankAccountOptions = await GetBankAccountOptionsAsync();

        if (model.ContactId.HasValue && model.ContactId.Value > 0)
        {
            var invoices = await QueryOpenInvoices(tenantId, model.ContactId.Value, model.PaymentType)
                .ToListAsync();

            var modelAllocDict = model.Allocations
                .GroupBy(a => a.InvoiceId)
                .ToDictionary(g => g.Key, g => g.First().Amount);

            model.Allocations = invoices.Select(i => new PaymentAllocationViewModel
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.Number,
                InvoiceTotal = i.TotalAmount,
                InvoicePaid = i.PaidAmount,
                InvoiceRemaining = InvoiceBalance.Remaining(i),
                InvoiceDate = FormatInvoiceDate(i.Date),
                Amount = modelAllocDict.TryGetValue(i.Id, out var amt) ? amt : 0
            }).ToList();
        }

        return View(model);
    }

    private IQueryable<MyFirstCoreApp.Domain.Entities.Invoicing.Invoice> QueryOpenInvoices(
        int tenantId, int contactId, PaymentType paymentType)
    {
        var invoiceType = paymentType == PaymentType.Received ? InvoiceType.Sale : InvoiceType.Purchase;

        return _context.Invoices
            .Where(i => i.TenantId == tenantId
                     && i.ContactId == contactId
                     && i.InvoiceType == invoiceType
                     && i.Status == InvoiceStatus.Posted)
            .WithOpenBalance()
            .OrderBy(i => i.Date);
    }

    // Invoice.Date تا مرحله‌ی 1E هنوز DateTime است؛ با همان فرمول پل (+۳:۳۰) شمسی می‌شود.
    private static string FormatInvoiceDate(DateOnly date)
        => PersianDate.Format(date);

    private async Task<List<SelectListItem>> GetContactOptionsAsync(PaymentType paymentType)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        var query = _context.Contacts.Where(c => c.TenantId == tenantId.Value && c.IsActive);

        if (paymentType == PaymentType.Received)
            query = query.Where(c => c.ContactType == ContactType.Customer || c.ContactType == ContactType.Both);
        else
            query = query.Where(c => c.ContactType == ContactType.Supplier || c.ContactType == ContactType.Both);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetBankAccountOptionsAsync()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return new();

        return await _context.Accounts
            .Where(a => a.TenantId == tenantId.Value && a.IsActive && a.AccountType == AccountType.Bank)
            .OrderBy(a => a.Name)
            .Select(a => new SelectListItem
            {
                Value = a.Id.ToString(),
                Text = a.Name + (a.BankName != null ? $" - {a.BankName}" : "")
            })
            .ToListAsync();
    }
}
