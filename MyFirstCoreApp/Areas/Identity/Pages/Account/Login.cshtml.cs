// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        public bool EmailNotConfirmed { get; set; }
        public bool PhoneNotConfirmed { get; set; }
        public string ConfirmationUserId { get; set; }
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
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
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [Display(Name = "ایمیل یا موبایل")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var identifier = NormalizeMobile(Input.Email.Trim());
                var user = await _userManager.FindByEmailAsync(Input.Email.Trim())
                    ?? await _userManager.FindByNameAsync(identifier);
                if (user == null)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "ایمیل یا کلمه عبور اشتباه است.");

                    return Page();
                }

                if (!user.IsActive)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "حساب کاربری شما غیرفعال است.");

                    return Page();
                }

                if (!user.EmailConfirmed && !user.PhoneNumberConfirmed)
                {
                    PhoneNotConfirmed = !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                        string.Equals(NormalizeMobile(user.PhoneNumber), identifier, StringComparison.Ordinal);
                    EmailNotConfirmed = !PhoneNotConfirmed && !string.IsNullOrWhiteSpace(user.Email);
                    ConfirmationUserId = PhoneNotConfirmed ? user.Id : null;
                    ModelState.AddModelError(
                        string.Empty,
                        "ایمیل یا موبایل شما هنوز تأیید نشده است. لطفاً ابتدا آن را تأیید کنید.");

                    return Page();
                }

                var result = await _signInManager.PasswordSignInAsync(user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    // اگه ReturnUrl مشخصه و local هست، برو همون‌جا
                    if (!string.IsNullOrEmpty(returnUrl)
                        && returnUrl != "/"
                        && Url.IsLocalUrl(returnUrl))
                    {
                        return LocalRedirect(returnUrl);
                    }

                    // 🆕 به Select برو (خودش تصمیم می‌گیره: Create / Select / Dashboard)
                    return RedirectToAction(
                        "Select",
                        "Tenant",
                        new { area = "Tenancy" });
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new
                    {
                        ReturnUrl = returnUrl,
                        Input.RememberMe
                    });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                ModelState.AddModelError(string.Empty, "ایمیل یا رمز عبور صحیح نیست.");
                return Page();
            }
                        return Page();
        }

        private static string NormalizeMobile(string value)
        {
            value = value.Trim();
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
}
