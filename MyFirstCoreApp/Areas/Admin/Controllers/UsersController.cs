using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Models.ViewModels;
using MyFirstCoreApp.Services;
using MyFirstCoreApp.Services.Interfaces;
using System.Text;
using System.Text.Encodings.Web;

namespace MyFirstCoreApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UsersController : Controller
    {
        private readonly IUserService _userService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        public UsersController(
            IUserService userService,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender)
        {
            _userService = userService;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [Authorize(Policy = "User.View")]
        public async Task<IActionResult> Index()
        {
            var users = await _userService.GetAllAsync();

            return View(users);
        }

        [HttpGet]
        [Authorize(Policy = "User.Create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "User.Create")]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Email,
                UserName = model.Email,
                PhoneNumber = model.PhoneNumber,
                IsActive = true
            };

            var result = await _userService.CreateAsync(
                user,
                model.Password);

            if (result.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userService.GetByIdAsync(id);

            if (user == null)
                return NotFound();

            var userRoles =
                await _userService.GetUserRolesAsync(user);

            var roles =
                await _userService.GetRolesForUserAsync(user);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                SelectedRoles = userRoles,
                Roles = roles
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> Edit(
            EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadRoles(model);
                return View(model);
            }

            var user = await _userService.GetByIdAsync(model.Id);

            if (user == null)
                return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.PhoneNumber = model.PhoneNumber;

            var updateResult =
                await _userService.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                AddErrors(updateResult);

                await LoadRoles(model);

                return View(model);
            }

            var rolesResult =
                await _userService.UpdateRolesAsync(
                    user,
                    model.SelectedRoles);

            if (!rolesResult.Succeeded)
            {
                AddErrors(rolesResult);

                await LoadRoles(model);

                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> ProfileImages(
    string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user =
                await _userService.GetByIdAsync(userId);

            if (user == null)
                return NotFound();

            var images =
                await _userService.GetProfileImagesAsync(userId);

            var model = new ProfileImagesViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Images = images
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> ProfileImage(int id)
        {
            var result =
                await _userService.GetProfileImageFileAsync(id);

            if (result == null)
                return NotFound();

            return PhysicalFile(
                result.Value.PhysicalPath,
                result.Value.ContentType);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> UploadProfileImages(
    string userId,
    List<IFormFile> NewImages)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return NotFound();

            var user =
                await _userService.GetByIdAsync(userId);

            if (user == null)
                return NotFound();

            if (NewImages == null || NewImages.Count == 0)
            {
                TempData["Error"] =
                    "حداقل یک تصویر انتخاب کنید.";

                return RedirectToAction(
                    nameof(ProfileImages),
                    new { userId });
            }

            foreach (var file in NewImages)
            {
                try
                {
                    await _userService.AddProfileImageAsync(
                        userId,
                        file);
                }
                catch (Exception ex)
                {
                    TempData["Error"] = ex.Message;

                    return RedirectToAction(
                        nameof(ProfileImages),
                        new { userId });
                }
            }

            TempData["Success"] =
                $"{NewImages.Count} تصویر با موفقیت آپلود شد.";

            return RedirectToAction(
                nameof(ProfileImages),
                new { userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> SetPrimaryProfileImage(
    int id)
        {
            var image =
                await _userService.GetProfileImageAsync(id);

            if (image == null)
                return NotFound();

            await _userService.SetPrimaryProfileImageAsync(id);

            return RedirectToAction(
                nameof(ProfileImages),
                new { userId = image.UserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> DeleteProfileImage(int id)
        {
            var image =
                await _userService.GetProfileImageAsync(id);

            if (image == null)
                return NotFound();

            await _userService.DeleteProfileImageAsync(id);

            return RedirectToAction(
                nameof(ProfileImages),
                new { userId = image.UserId });
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }
        }

        private async Task LoadRoles(
    EditUserViewModel model)
        {
            if (string.IsNullOrEmpty(model.Id))
                return;

            var user =
                await _userService.GetByIdAsync(model.Id);

            if (user == null)
                return;

            model.Roles =
                await _userService.GetRolesForUserAsync(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> ToggleActive(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userService.GetByIdAsync(id);

            if (user == null)
                return NotFound();

            var result =
                await _userService.ToggleActiveAsync(user);

            if (!result.Succeeded)
            {
                AddErrors(result);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "User.Edit")]
        public async Task<IActionResult> ResendConfirmationEmail(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["ErrorMessage"] = "کاربر پیدا نشد.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                TempData["ErrorMessage"] =
                    "ایمیل این کاربر قبلاً تأیید شده است.";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                var userId = await _userManager.GetUserIdAsync(user);

                var code =
                    await _userManager.GenerateEmailConfirmationTokenAsync(user);

                code = WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new
                    {
                        area = "Identity",
                        userId = userId,
                        code = code
                    },
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
                    سلام {HtmlEncoder.Default.Encode(user.FullName)}،
                </p>

                <p>
                    برای تأیید آدرس ایمیل حساب کاربری خود
                    روی دکمه زیر کلیک کنید:
                </p>

                <p style="margin:30px 0;">

                    <a href="{HtmlEncoder.Default.Encode(callbackUrl!)}"
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

                <hr />

                <small>
                    MyFirstCoreApp
                </small>

            </div>
            """;

                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "تأیید آدرس ایمیل - MyFirstCoreApp",
                    emailBody);

                TempData["SuccessMessage"] =
                    $"لینک تأیید به {user.Email} ارسال شد.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    $"خطا در ارسال ایمیل: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}