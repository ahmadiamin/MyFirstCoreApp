using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Areas.Tenancy.Controllers;

/// <summary>
/// «با چه سمتی وارد می‌شوی؟» فقط برای کسی که هم مدیر یک کسب‌وکار است و هم کارمند جای دیگر.
/// اگر فقط یک سمت دارد، سؤالی پرسیده نمی‌شود.
/// </summary>
[Area("Tenancy")]
[Authorize]
public class CapacityController : Controller
{
    private readonly ILoginCapacityService _capacityService;
    private readonly ILoginCapacityAccessor _accessor;

    public CapacityController(ILoginCapacityService capacityService, ILoginCapacityAccessor accessor)
    {
        _capacityService = capacityService;
        _accessor = accessor;
    }

    [HttpGet]
    public async Task<IActionResult> Choose()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var info = await _capacityService.GetAsync(userId);

        // یک سمت یا هیچ: چیزی برای پرسیدن نیست
        if (!info.NeedsChoice)
            return RedirectToAction("Index", "Dashboard", new { area = "" });

        return View(info);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Choose(LoginCapacity capacity)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var info = await _capacityService.GetAsync(userId);

        // فقط سمتی که واقعاً دارد (انتخاب سمتِ ناداشته = دستکاری فرم)
        var allowed = capacity == LoginCapacity.Owner ? info.HasOwner : info.HasEmployee;
        if (!Enum.IsDefined(capacity) || !allowed)
            return BadRequest();

        _accessor.Set(userId, capacity);

        return RedirectToAction("Index", "Dashboard", new { area = "" });
    }
}
