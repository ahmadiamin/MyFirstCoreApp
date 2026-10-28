#nullable disable
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Areas.Identity.Pages.Account;

[EnableRateLimiting("RegistrationSms")]
public sealed class ConfirmPhoneModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ISystemSmsService _systemSms;

    public ConfirmPhoneModel(UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, ISystemSmsService systemSms)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _systemSms = systemSms;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required]
        public string UserId { get; set; }
        [Required]
        [StringLength(10, MinimumLength = 4)]
        [Display(Name = "کد تأیید")]
        public string Code { get; set; }
        public string ReturnUrl { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string userId, string returnUrl = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.PhoneNumber)) return RedirectToPage("./Register");
        Input = new InputModel { UserId = user.Id, ReturnUrl = returnUrl };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _userManager.FindByIdAsync(Input.UserId);
        if (user is null || string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            ModelState.AddModelError(string.Empty, "اطلاعات ثبت‌نام پیدا نشد.");
            return Page();
        }

        var code = NormalizeDigits(Input.Code.Trim());
        var result = await _userManager.ChangePhoneNumberAsync(user, user.PhoneNumber, code);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("Input.Code", "کد تأیید نامعتبر یا منقضی شده است.");
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        if (!string.IsNullOrWhiteSpace(Input.ReturnUrl) && Input.ReturnUrl != "/" && Url.IsLocalUrl(Input.ReturnUrl))
            return LocalRedirect(Input.ReturnUrl);
        return RedirectToAction("Select", "Tenant", new { area = "Tenancy" });
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.UserId)) return RedirectToPage("./Register");
        var user = await _userManager.FindByIdAsync(Input.UserId);
        if (user is null || user.PhoneNumberConfirmed || string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            ModelState.AddModelError(string.Empty, "امکان ارسال مجدد کد وجود ندارد.");
            return Page();
        }

        var code = await _userManager.GenerateChangePhoneNumberTokenAsync(user, user.PhoneNumber);
        var sent = await _systemSms.SendRegistrationCodeAsync(user.PhoneNumber, code);
        if (!sent.IsAccepted) ModelState.AddModelError(string.Empty, sent.Message);
        else ViewData["Message"] = "کد تأیید دوباره ارسال شد.";
        return Page();
    }

    private static string NormalizeDigits(string value)
    {
        var result = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= '\u06F0' and <= '\u06F9') result.Append((char)('0' + character - '\u06F0'));
            else if (character is >= '\u0660' and <= '\u0669') result.Append((char)('0' + character - '\u0660'));
            else result.Append(character);
        }
        return result.ToString();
    }
}
