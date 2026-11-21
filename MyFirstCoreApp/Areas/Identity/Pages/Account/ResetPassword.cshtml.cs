using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using MyFirstCoreApp.Domain.Entities.Identity;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MyFirstCoreApp.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            public string Code { get; set; } = string.Empty;

            [Required(ErrorMessage = "لطفاً ایمیل را وارد کنید.")]
            [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "لطفاً رمز عبور جدید را وارد کنید.")]
            [StringLength(
                100,
                ErrorMessage = "رمز عبور باید حداقل {2} و حداکثر {1} کاراکتر باشد.",
                MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Compare(
                "Password",
                ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public IActionResult OnGet(string? code = null)
        {
            if (code == null)
            {
                return BadRequest("کد بازیابی رمز عبور ارسال نشده است.");
            }

            Input.Code = code;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            string decodedCode;
            try
            {
                decodedCode = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(Input.Code));
            }
            catch (FormatException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "لینک بازیابی نامعتبر یا ناقص است. لطفاً دوباره درخواست بازیابی رمز عبور بدهید.");
                return Page();
            }

            var result = await _userManager.ResetPasswordAsync(
                user,
                decodedCode,
                Input.Password);

            if (result.Succeeded)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }

            return Page();
        }
    }
}
