using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Contacts.ViewModels.CustomerPortal;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Contacts.Controllers;

[Area("Contacts")]
[Authorize]
public sealed class CustomerPortalLinksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _tenant;
    private readonly ICustomerPortalService _portal;
    private readonly IFarazSmsService _sms;
    private readonly TimeProvider _timeProvider;

    public CustomerPortalLinksController(ApplicationDbContext context, ICurrentTenant tenant,
        ICustomerPortalService portal, IFarazSmsService sms, TimeProvider timeProvider)
    {
        _context = context;
        _tenant = tenant;
        _portal = portal;
        _sms = sms;
        _timeProvider = timeProvider;
    }

    [Authorize(Policy = "CustomerPortalLink.View")]
    public async Task<IActionResult> Index(int contactId)
    {
        if (!_tenant.TenantId.HasValue)
            return Forbid();

        var tenantId = _tenant.TenantId.Value;
        var contact = await _context.Contacts.AsNoTracking()
            .Where(x => x.Id == contactId && x.TenantId == tenantId && x.IsActive)
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync();
        if (contact is null) 
            return NotFound();

        var model = new CustomerPortalLinksViewModel
        {
            ContactId = contact.Id,
            ContactName = contact.Name,
            ExpiresOn = PersianDate.Format(_timeProvider.Today().AddDays(30)),
            Links = await _portal.GetSharesAsync(tenantId, contactId),
            CreatedUrl = TempData["CreatedPortalUrl"] as string,
            SmsNotice = TempData["SmsNotice"] as string
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CustomerPortalLink.Sms")]
    public async Task<IActionResult> SendSms(int contactId, int id, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var tenantId = _tenant.TenantId.Value;
        var link = (await _portal.GetSharesAsync(tenantId, contactId))
            .FirstOrDefault(x => x.Id == id);
        if (link is null) return NotFound();
        if (string.IsNullOrWhiteSpace(link.Token))
        {
            TempData["Error"] = "این لینک قدیمی است و توکن آن قابل بازیابی نیست؛ ابتدا لینک تازه‌ای بسازید.";
            return RedirectToAction(nameof(Index), new { contactId });
        }

        var mobile = await _context.Contacts.AsNoTracking()
            .Where(x => x.Id == contactId && x.TenantId == tenantId && !x.IsDeleted && x.IsActive)
            .Select(x => x.Mobile)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(mobile))
        {
            TempData["Error"] = "برای این مشتری شماره موبایل ثبت نشده است.";
            return RedirectToAction(nameof(Index), new { contactId });
        }
        mobile = mobile.Replace("-", "");

        var url = Url.Action("Index", "CustomerPortal", new { area = "", token = link.Token }, Request.Scheme);
        if (string.IsNullOrWhiteSpace(url))
        {
            TempData["Error"] = "ساخت لینک پرتال انجام نشد.";
            return RedirectToAction(nameof(Index), new { contactId });
        }

        var result = await _sms.SendPortalLinkAsync(tenantId, contactId, id, mobile, url,
            User.Identity?.Name, cancellationToken);
        TempData[result.IsAccepted ? "SmsNotice" : "Error"] = result.RequestId is null
            ? result.Message
            : $"{result.Message} شناسه پیگیری: {result.RequestId}";
        return RedirectToAction(nameof(Index), new { contactId });
    }

    [Authorize(Policy = "CustomerPortalLink.View")]
    public async Task<IActionResult> SmsStatus(int contactId, int id, CancellationToken cancellationToken)
    {
        if (!_tenant.TenantId.HasValue) return Forbid();
        var link = (await _portal.GetSharesAsync(_tenant.TenantId.Value, contactId))
            .FirstOrDefault(x => x.Id == id);
        if (link is null) return NotFound();
        var report = await _sms.GetLatestDeliveryReportAsync(
            _tenant.TenantId.Value, contactId, id, cancellationToken);
        if (report is null) return NotFound();
        return View("SmsStatus", new MyFirstCoreApp.Application.CustomerPortal.CustomerPortalSmsStatusViewModel
        {
            ContactId = contactId,
            ShareId = id,
            ContactName = link.ContactName,
            Report = report
        });
    }

    [Authorize(Policy = "CustomerPortalLink.View")]
    public async Task<IActionResult> Edit(int contactId, int id)
    {
        if (!_tenant.TenantId.HasValue) 
            return Forbid();

        var link = (await _portal.GetSharesAsync(_tenant.TenantId.Value, contactId))
            .FirstOrDefault(x => x.Id == id);
        if (link is null) 
            return NotFound();

        return View(new CustomerPortalShareEditViewModel
        {
            ContactId = contactId,
            Id = link.Id,
            ContactName = link.ContactName,
            CanViewLedger = link.CanViewLedger,
            CanViewSalesInvoices = link.CanViewSalesInvoices,
            ExpiresOn = link.ExpiresAtUtc.HasValue
                ? PersianDate.Format(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(link.ExpiresAtUtc.Value, DateTimeKind.Utc), _timeProvider.LocalTimeZone)))
                : null,
            PortalUrl = link.Token is null ? null : Url.Action("Index", "CustomerPortal",
                new { area = "", token = link.Token }, Request.Scheme)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CustomerPortalLink.Edit")]
    public async Task<IActionResult> Edit(CustomerPortalShareEditViewModel model)
    {
        if (!_tenant.TenantId.HasValue) 
            return Forbid();

        DateTime? expiresAtUtc = null;
        if (!string.IsNullOrWhiteSpace(model.ExpiresOn))
        {
            if (!PersianDate.TryParse(model.ExpiresOn, out var expiresOn))
                ModelState.AddModelError(nameof(model.ExpiresOn), "تاریخ انقضا معتبر نیست.");
            else if (expiresOn <= _timeProvider.Today())
                ModelState.AddModelError(nameof(model.ExpiresOn), "تاریخ انقضا باید بعد از امروز باشد.");
            else
                expiresAtUtc = TimeZoneInfo.ConvertTimeToUtc(
                    expiresOn.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified), _timeProvider.LocalTimeZone);
        }

        if (!model.CanViewLedger && !model.CanViewSalesInvoices)
            ModelState.AddModelError(string.Empty, "حداقل یک نوع دسترسی را انتخاب کنید.");

        if (ModelState.IsValid)
        {
            try
            {
                await _portal.UpdateShareAsync(_tenant.TenantId.Value, model.ContactId, model.Id,
                    new Application.CustomerPortal.CustomerPortalShareUpdate
                    {
                        CanViewLedger = model.CanViewLedger,
                        CanViewSalesInvoices = model.CanViewSalesInvoices,
                        ExpiresAtUtc = expiresAtUtc,
                        UpdatedBy = User.Identity?.Name
                    });
                TempData["Success"] = "تنظیمات لینک به‌روزرسانی شد.";
                return RedirectToAction(nameof(Index), new { contactId = model.ContactId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        var link = (await _portal.GetSharesAsync(_tenant.TenantId.Value, model.ContactId))
            .FirstOrDefault(x => x.Id == model.Id);
        if (link is null) return NotFound();
        model.ContactName = link.ContactName;
        model.PortalUrl = link.Token is null ? null : Url.Action("Index", "CustomerPortal",
            new { area = "", token = link.Token }, Request.Scheme);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CustomerPortalLink.Create")]
    public async Task<IActionResult> Create(CustomerPortalLinksViewModel model)
    {
        if (!_tenant.TenantId.HasValue) 
            return Forbid();

        DateTime? expiresAtUtc = null;
        if (!string.IsNullOrWhiteSpace(model.ExpiresOn))
        {
            if (!PersianDate.TryParse(model.ExpiresOn, out var expiresOn))
                ModelState.AddModelError(nameof(model.ExpiresOn), "تاریخ انقضا معتبر نیست.");
            else if (expiresOn <= _timeProvider.Today())
                ModelState.AddModelError(nameof(model.ExpiresOn), "تاریخ انقضا باید بعد از امروز باشد.");
            else
            {
                var localEnd = expiresOn.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified);
                expiresAtUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, _timeProvider.LocalTimeZone);
            }
        }
        if (!model.CanViewLedger && !model.CanViewSalesInvoices)
            ModelState.AddModelError(string.Empty, "حداقل یک نوع دسترسی را انتخاب کنید.");

        if (!ModelState.IsValid)
            return await ReturnIndexWithErrorsAsync(model);

        try
        {
            var created = await _portal.CreateShareAsync(_tenant.TenantId.Value, model.ContactId,
                model.CanViewLedger, model.CanViewSalesInvoices, expiresAtUtc, User.Identity?.Name);
            var url = Url.Action("Index", "CustomerPortal", new { token = created.Token }, Request.Scheme);
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("ساخت لینک پرتال انجام نشد.");
            TempData["CreatedPortalUrl"] = url;
            return RedirectToAction(nameof(Index), new { contactId = model.ContactId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await ReturnIndexWithErrorsAsync(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "CustomerPortalLink.Revoke")]
    public async Task<IActionResult> Revoke(int contactId, int id)
    {
        if (!_tenant.TenantId.HasValue)
            return Forbid();

        try
        {
            await _portal.RevokeShareAsync(_tenant.TenantId.Value, id);
            TempData["Success"] = "لینک دسترسی لغو شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { contactId });
    }

    private async Task<IActionResult> ReturnIndexWithErrorsAsync(CustomerPortalLinksViewModel model)
    {
        if (!_tenant.TenantId.HasValue) 
            return Forbid();

        var contact = await _context.Contacts.AsNoTracking()
            .Where(x => x.Id == model.ContactId && x.TenantId == _tenant.TenantId.Value)
            .Select(x => x.Name).FirstOrDefaultAsync();
        if (contact is null)
            return NotFound();

        model.ContactName = contact;
        model.Links = await _portal.GetSharesAsync(_tenant.TenantId.Value, model.ContactId);
        return View("Index", model);
    }
}
