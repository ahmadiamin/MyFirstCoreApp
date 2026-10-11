using Microsoft.AspNetCore.Mvc;
using MyFirstCoreApp.Services.Interfaces;

namespace MyFirstCoreApp.ViewComponents
{
    public class AdminMenuViewComponent : ViewComponent
    {
        private readonly IMenuService _menuService;

        public AdminMenuViewComponent(IMenuService menuService)
        {
            _menuService = menuService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = ViewContext.HttpContext.User;

            var menus = await _menuService.GetUserMenusAsync(user);

            return View(menus);
        }
    }
}