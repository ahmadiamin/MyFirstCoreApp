using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.CustomerPortal;

namespace MyFirstCoreApp.Controllers;

[AllowAnonymous]
[Route("customer-portal")]
public sealed class CustomerPortalController : Controller
{
    private readonly ICustomerPortalService _portal;
    private readonly TimeProvider _timeProvider;

    public CustomerPortalController(ICustomerPortalService portal, TimeProvider timeProvider)
    {
        _portal = portal;
        _timeProvider = timeProvider;
    }

    [HttpGet("{token}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(string token, int page = 1)
    {
        SetPrivateResponseHeaders();
        var data = await _portal.GetPortalAsync(token, _timeProvider.Today(), page);
        return data is null ? NotFound() : View("Index", new CustomerPortalPublicViewModel(token, data));
    }

    [HttpGet("{token}/invoices/{invoiceId:int}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Invoice(string token, int invoiceId)
    {
        SetPrivateResponseHeaders();
        var data = await _portal.GetInvoiceAsync(token, invoiceId);
        return data is null ? NotFound() : View("Invoice", new CustomerPortalInvoiceViewModel(token, data));
    }

    private void SetPrivateResponseHeaders()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
    }
}

public sealed record CustomerPortalPublicViewModel(string Token, CustomerPortalPage Data);
public sealed record CustomerPortalInvoiceViewModel(string Token, CustomerPortalInvoiceDetails Data);
