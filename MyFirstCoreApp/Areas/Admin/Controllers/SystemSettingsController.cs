using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Areas.Admin.ViewModels;

namespace MyFirstCoreApp.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin")]
public sealed class SystemSettingsController : Controller
{
    private readonly ISystemSmsService _sms;
    private readonly ICurrentUser _currentUser;

    public SystemSettingsController(ISystemSmsService sms, ICurrentUser currentUser)
    {
        _sms = sms;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Sms(CancellationToken cancellationToken)
    {
        var summary = await _sms.GetSummaryAsync(cancellationToken);
        return View(new SystemSmsSettingsViewModel
        {
            ApiKeyConfigured = summary.ApiKeyConfigured,
            LineNumber = summary.LineNumber,
            RegistrationSmsEnabled = summary.RegistrationSmsEnabled,
            RegistrationSmsPatternCode = summary.RegistrationSmsPatternCode
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sms(SystemSmsSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (model.RegistrationSmsEnabled &&
            string.IsNullOrWhiteSpace(model.RegistrationSmsPatternCode))
            ModelState.AddModelError(nameof(model.RegistrationSmsPatternCode), "کد الگوی ثبت‌شده در پنل FarazSMS الزامی است.");

        var summary = await _sms.GetSummaryAsync(cancellationToken);
        if (!summary.ApiKeyConfigured && string.IsNullOrWhiteSpace(model.ApiKeyInput))
            ModelState.AddModelError(nameof(model.ApiKeyInput), "کلید API پیامک سامانه را وارد کنید.");
        if (model.RegistrationSmsEnabled && model.ClearApiKey && string.IsNullOrWhiteSpace(model.ApiKeyInput))
            ModelState.AddModelError(nameof(model.ApiKeyInput), "برای فعال‌ماندن تأیید موبایل، کلید API را پاک نکنید.");

        if (!ModelState.IsValid)
        {
            model.ApiKeyConfigured = summary.ApiKeyConfigured;
            return View(model);
        }

        try
        {
            await _sms.SaveAsync(model.ApiKeyInput, model.ClearApiKey, model.LineNumber,
                model.RegistrationSmsEnabled, model.RegistrationSmsPatternCode, _currentUser.UserId,
                cancellationToken);
            TempData["Success"] = "تنظیمات پیامک سامانه ذخیره شد.";
            return RedirectToAction(nameof(Sms));
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            model.ApiKeyConfigured = summary.ApiKeyConfigured;
            return View(model);
        }
    }
}
