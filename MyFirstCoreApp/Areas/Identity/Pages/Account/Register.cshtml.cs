// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Areas.Identity.Pages.Account
{
    [EnableRateLimiting("RegistrationSms")]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ISystemSmsService _systemSms;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ISystemSmsService systemSms)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _systemSms = systemSms;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            [Required]
            [Display(Name = "نام و نام خانوادگی")]
            public string FullName { get; set; } = string.Empty;

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [EmailAddress]
            [Display(Name = "ایمیل")]
            public string Email { get; set; }

            [Display(Name = "موبایل")]
            public string PhoneNumber { get; set; }

            [Required]
            [Display(Name = "روش احراز هویت")]
            public string VerificationMethod { get; set; } = "Email";

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "طول عبارت وارد شده {0}، باید حداقل {2} و حداکثر {1} کاراکتر باشد.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "رمز عبور و تکرار رمز عبور مطابقت ندارند.")]
            public string ConfirmPassword { get; set; }
        }


        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            Input ??= new InputModel();
            var useSms = string.Equals(Input.VerificationMethod, "Sms", StringComparison.OrdinalIgnoreCase);
            var email = Input.Email?.Trim();
            var mobile = NormalizeMobile(Input.PhoneNumber);

            if (!useSms && !string.Equals(Input.VerificationMethod, "Email", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError("Input.VerificationMethod", "روش احراز هویت معتبر نیست.");

            if (useSms && !System.Text.RegularExpressions.Regex.IsMatch(mobile, "^09[0-9]{9}$"))
                ModelState.AddModelError("Input.PhoneNumber", "برای تأیید پیامکی، موبایل باید ۱۱ رقم و با 09 شروع شود.");
            if (!useSms && string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("Input.Email", "برای تأیید ایمیلی، ایمیل الزامی است.");
            if (useSms)
            {
                var smsSettings = await _systemSms.GetSummaryAsync();
                if (!smsSettings.RegistrationSmsEnabled || !smsSettings.ApiKeyConfigured)
                    ModelState.AddModelError(string.Empty, "تأیید موبایل در حال حاضر فعال نیست؛ روش ایمیل را انتخاب کنید.");
                else if (await _userManager.Users.AnyAsync(x => x.PhoneNumber == mobile))
                    ModelState.AddModelError("Input.PhoneNumber", "این شماره موبایل قبلاً ثبت شده است.");
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();
                user.FullName = Input.FullName;
                user.PhoneNumber = string.IsNullOrWhiteSpace(mobile) ? null : mobile;
                var username = useSms ? mobile : email!;
                await _userStore.SetUserNameAsync(user, username, CancellationToken.None);
                if (!string.IsNullOrWhiteSpace(email))
                    await _emailStore.SetEmailAsync(user, email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var ownerRoleResult = await _userManager.AddToRoleAsync(user, "TenantOwner");
                    if (!ownerRoleResult.Succeeded)
                    {
                        await _userManager.DeleteAsync(user);
                        foreach (var error in ownerRoleResult.Errors)
                            ModelState.AddModelError(string.Empty, error.Description);
                        return Page();
                    }

                    if (useSms)
                    {
                        var verificationCode = await _userManager.GenerateChangePhoneNumberTokenAsync(user, mobile);
                        var sendResult = await _systemSms.SendRegistrationCodeAsync(mobile, verificationCode);
                        if (!sendResult.IsAccepted)
                        {
                            await _userManager.DeleteAsync(user);
                            ModelState.AddModelError(string.Empty, sendResult.Message);
                            return Page();
                        }

                        return RedirectToPage("./ConfirmPhone", new { userId = user.Id, returnUrl });
                    }

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    var emailBody = $"""
    <div style="
        direction:rtl;
        text-align:right;
        font-family:Tahoma,Arial;
        line-height:2;">

        <h2 style="color:#007bff;">
            تأیید آدرس ایمیل
        </h2>

        <p>
            سلام {HtmlEncoder.Default.Encode(Input.FullName)}،
        </p>

        <p>
            حساب کاربری شما در <strong>MyFirstCoreApp</strong>
            با موفقیت ایجاد شد.
        </p>

        <p>
            برای فعال‌سازی حساب کاربری و تأیید آدرس ایمیل،
            روی دکمه زیر کلیک کنید:
        </p>

        <p style="margin:30px 0;">

            <a href="{HtmlEncoder.Default.Encode(callbackUrl)}"
               style="
                   display:inline-block;
                   padding:12px 25px;
                   background:#007bff;
                   color:#ffffff;
                   text-decoration:none;
                   border-radius:5px;">

                تأیید ایمیل

            </a>

        </p>

        <p>
            اگر شما این حساب کاربری را ایجاد نکرده‌اید،
            این ایمیل را نادیده بگیرید.
        </p>

        <hr />

        <small>
            MyFirstCoreApp
        </small>

    </div>
    """;

                    await _emailSender.SendEmailAsync(
                        Input.Email,
                        "تأیید آدرس ایمیل - MyFirstCoreApp",
                        emailBody);

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return Redirect($"/Identity/Account/RegisterConfirmation?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private static string NormalizeMobile(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
            var result = new StringBuilder(mobile.Length);
            foreach (var character in mobile.Trim())
            {
                if (character is >= '\u06F0' and <= '\u06F9') result.Append((char)('0' + character - '\u06F0'));
                else if (character is >= '\u0660' and <= '\u0669') result.Append((char)('0' + character - '\u0660'));
                else result.Append(character);
            }
            return result.ToString();
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
