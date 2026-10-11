using MyFirstCoreApp.Models.ViewModels;
using System.Security.Claims;

namespace MyFirstCoreApp.Services.Interfaces
{
    public interface IMenuService
    {
        Task<List<MenuViewModel>> GetUserMenusAsync(
            ClaimsPrincipal user);
    }
}