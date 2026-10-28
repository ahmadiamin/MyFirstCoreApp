using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Models.ViewModels;
using MyFirstCoreApp.Services.Interfaces;
using System.Security.Claims;

namespace MyFirstCoreApp.Authorization
{
    public class MenuService : IMenuService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;

        public MenuService(
            ApplicationDbContext context,
            IPermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        public async Task<List<MenuViewModel>> GetUserMenusAsync(
            ClaimsPrincipal user)
        {
            var menus = await _context.Menus
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var visibleMenus = new List<Menu>();

            foreach (var menu in menus)
            {
                if (string.IsNullOrWhiteSpace(menu.Permission))
                {
                    visibleMenus.Add(menu);
                    continue;
                }

                if (await _permissionService.HasPermissionAsync(
                    user,
                    menu.Permission))
                {
                    visibleMenus.Add(menu);
                }
            }

            var menuLookup = visibleMenus
                .ToLookup(x => x.ParentId);

            List<MenuViewModel> BuildMenu(int? parentId)
            {
                return menuLookup[parentId]
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new MenuViewModel
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Url = x.Url,
                        Icon = x.Icon,
                        Children = BuildMenu(x.Id)
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Url) || x.Children.Count > 0)
                    .ToList();
            }

            return BuildMenu(null);
        }
    }
}
