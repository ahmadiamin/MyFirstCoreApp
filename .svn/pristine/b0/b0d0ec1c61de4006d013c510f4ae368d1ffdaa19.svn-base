using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Models.ViewModels;
using MyFirstCoreApp.Services.Interfaces;

namespace MyFirstCoreApp.ViewComponents
{
    public class UserProfileViewComponent : ViewComponent
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserService _userService;

        public UserProfileViewComponent(
            UserManager<ApplicationUser> userManager,
            IUserService userService)
        {
            _userManager = userManager;
            _userService = userService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (User.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var user =
                await _userManager.GetUserAsync(UserClaimsPrincipal);

            if (user == null)
                return Content(string.Empty);

            var images =
                await _userService.GetProfileImagesAsync(user.Id);

            var primaryImage =
                images.FirstOrDefault(x => x.IsPrimary);

            var model = new UserProfileViewModel
            {
                FullName = user.FullName,
                PrimaryImageId = primaryImage?.Id
            };

            return View(model);
        }
    }
}