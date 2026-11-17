using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Models.ViewModels;
using MyFirstCoreApp.Services.Interfaces;
using System.Security.Claims;

namespace MyFirstCoreApp.Authorization
{
    /// <summary>
    /// ادغام ASP.NET با IMenuTreeService: کاربر و «شرکت فعال» را از درخواست می‌خواند.
    /// منطق فیلتر (سراسری / سطح حساب / سطح شرکت) در Infrastructure است و جداگانه تست می‌شود.
    /// </summary>
    public class MenuService : IMenuService
    {
        private readonly IMenuTreeService _menuTree;
        private readonly ICurrentTenant _currentTenant;

        public MenuService(
            IMenuTreeService menuTree,
            ICurrentTenant currentTenant)
        {
            _menuTree = menuTree;
            _currentTenant = currentTenant;
        }
        public async Task<List<MenuViewModel>> GetUserMenusAsync(ClaimsPrincipal user)

        {
            var subject = new PermissionSubject(
                IsAuthenticated: user.Identity?.IsAuthenticated == true,
                IsSuperAdmin: user.IsInRole("SuperAdmin"),
                UserId: user.FindFirstValue(ClaimTypes.NameIdentifier),
                TenantId: _currentTenant.TenantId);

            var tree = await _menuTree.GetVisibleTreeAsync(subject);

            return tree.Select(ToViewModel).ToList();
        }
        private static MenuViewModel ToViewModel(MenuNode node) => new()
        {
            Id = node.Id,
            Title = node.Title,
            Url = node.Url,
            Icon = node.Icon,
            Children = node.Children.Select(ToViewModel).ToList()
        };
    }
}