
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Services;

namespace MyFirstCoreApp.Areas.Identity.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordResetOtpService _otpService;

    public ForgotPasswordModel(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IPasswordResetOtpService otpService)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _otpService = otpService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "لطفاً ایمیل یا شماره موبایل را وارد کنید.")]
        [StringLength(256, ErrorMessage = "مقدار واردشده بیش از حد مجاز است.")]
        public string EmailOrPhone { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        var value = Input.EmailOrPhone.Trim();

        var isMobile = !value.Contains('@');

        if (isMobile)
        {
            var result = await _otpService.RequestCodeAsync(
                value,
                cancellationToken);

            // برای جلوگیری از افشای وجود یا عدم وجود حساب کاربری،
            // در هر دو حالت به صفحه تأیید هدایت می‌شویم.
            TempData["RecoveryMobile"] = NormalizeDigits(value);

            if (!result.IsAccepted)
            {
                // در این مرحله پیام عمومی نمایش داده می‌شود.
                // جزئیات خطا نباید وجود حساب کاربری را افشا کند.
            }

            return RedirectToPage("./VerifyResetCode");
        }

        if (!new EmailAddressAttribute().IsValid(value))
        {
            ModelState.AddModelError(
                "Input.EmailOrPhone",
                "فرمت ایمیل صحیح نیست.");

            return Page();
        }

        var user = await _userManager.FindByEmailAsync(value);

        if (user is null ||
            !await _userManager.IsEmailConfirmedAsync(user))
        {
            return RedirectToPage("./ForgotPasswordConfirmation");
        }

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);

        var encodedCode = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(code));

        var callbackUrl = Url.Page(
            "/Account/ResetPassword",
            pageHandler: null,
            values: new
            {
                area = "Identity",
                userId = user.Id,
                code = encodedCode
            },
            protocol: Request.Scheme);

        if (string.IsNullOrWhiteSpace(callbackUrl))
            return RedirectToPage("./ForgotPasswordConfirmation");

        var safeUrl = HtmlEncoder.Default.Encode(callbackUrl);

        var emailBody = $"""
            <div style="direction:rtl;text-align:right;font-family:Tahoma,Arial;">
                <h2>بازیابی کلمه عبور</h2>
                <p>سلام،</p>
                <p>
                    برای تغییر کلمه عبور حساب کاربری خود
                    روی لینک زیر کلیک کنید:
                </p>
                <p>
                    <a href="{safeUrl}"
                       style="display:inline-block;padding:10px 20px;
                              background:#007bff;color:white;
                              text-decoration:none;border-radius:4px;">
                        تغییر کلمه عبور
                    </a>
                </p>
                <p>
                    اگر شما این درخواست را ارسال نکرده‌اید،
                    این ایمیل را نادیده بگیرید.
                </p>
                <hr />
                <small>MyFirstCoreApp</small>
            </div>
            """;

        await _emailSender.SendEmailAsync(
            user.Email!,
            "بازیابی کلمه عبور - MyFirstCoreApp",
            emailBody);

        return RedirectToPage("./ForgotPasswordConfirmation");
    }

    private static string NormalizeDigits(string value)
    {
        var result = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is >= '\u06F0' and <= '\u06F9')
                result.Append((char)('0' + character - '\u06F0'));
            else if (character is >= '\u0660' and <= '\u0669')
                result.Append((char)('0' + character - '\u0660'));
            else
                result.Append(character);
        }

        return result.ToString();
    }
}
