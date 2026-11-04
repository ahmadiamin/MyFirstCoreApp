using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyFirstCoreApp.Application.Interfaces;

namespace MyFirstCoreApp.Areas.Identity.Pages.Account;

public class VerifyResetCodeModel : PageModel
{
    private readonly IPasswordResetOtpService _otpService;

    public VerifyResetCodeModel(IPasswordResetOtpService otpService)
    {
        _otpService = otpService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "لطفاً کد پیامک‌شده را وارد کنید.")]
        [StringLength(6, MinimumLength = 6,
            ErrorMessage = "کد تأیید باید ۶ رقم باشد.")]
        [RegularExpression(@"^[0-9۰-۹٠-٩]{6}$",
            ErrorMessage = "کد تأیید باید شامل ۶ رقم باشد.")]
        public string Code { get; set; } = string.Empty;
    }

    public string Mobile { get; private set; } = string.Empty;

    public void OnGet()
    {
        if (!TryGetMobile(out var mobile))
        {
            Response.Redirect(Url.Page("./ForgotPassword")!);
            return;
        }

        Mobile = mobile;
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetMobile(out var mobile))
            return RedirectToPage("./ForgotPassword");

        Mobile = mobile;

        if (!ModelState.IsValid)
            return Page();

        var result = await _otpService.VerifyCodeAsync(
            mobile,
            Input.Code,
            cancellationToken);

        if (!result.IsVerified || result.RequestId is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Message);

            return Page();
        }

        TempData["VerifiedOtpRequestId"] = result.RequestId.Value;
        TempData.Remove("RecoveryMobile");

        return RedirectToPage("./ResetPasswordByOtp");
    }

    public async Task<IActionResult> OnPostResendAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetMobile(out var mobile))
            return RedirectToPage("./ForgotPassword");

        Mobile = mobile;

        var result = await _otpService.RequestCodeAsync(
            mobile,
            cancellationToken);

        TempData["OtpResendMessage"] = result.Message;
        TempData["OtpResendAccepted"] = result.IsAccepted;

        return RedirectToPage();
    }

    private bool TryGetMobile(out string mobile)
    {
        mobile = TempData.Peek("RecoveryMobile") as string ?? string.Empty;

        return !string.IsNullOrWhiteSpace(mobile);
    }
}