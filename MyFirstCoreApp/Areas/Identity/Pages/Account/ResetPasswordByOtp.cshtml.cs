using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyFirstCoreApp.Application.Interfaces;

namespace MyFirstCoreApp.Areas.Identity.Pages.Account;

public class ResetPasswordByOtpModel : PageModel
{
    private readonly IPasswordResetOtpService _otpService;

    public ResetPasswordByOtpModel(IPasswordResetOtpService otpService)
    {
        _otpService = otpService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "لطفاً رمز عبور جدید را وارد کنید.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "لطفاً تکرار رمز عبور را وارد کنید.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword),
            ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (!TryGetRequestId(out _))
            return RedirectToPage("./ForgotPassword");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!TryGetRequestId(out var requestId))
            return RedirectToPage("./ForgotPassword");

        if (!ModelState.IsValid)
            return Page();

        var result = await _otpService.CompletePasswordResetAsync(
            requestId,
            Input.NewPassword,
            cancellationToken);

        if (!result.IsSucceeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);

            return Page();
        }

        TempData.Remove("VerifiedOtpRequestId");

        return RedirectToPage("./ResetPasswordConfirmation");
    }

    private bool TryGetRequestId(out int requestId)
    {
        requestId = 0;

        var value = TempData.Peek("VerifiedOtpRequestId");

        if (value is null)
            return false;

        if (!int.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                out requestId))
        {
            return false;
        }

        return requestId > 0;
    }
}