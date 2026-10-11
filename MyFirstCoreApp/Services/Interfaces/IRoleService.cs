using Microsoft.AspNetCore.Identity;
using MyFirstCoreApp.Models.ViewModels;

namespace MyFirstCoreApp.Services.Interfaces
{
    public interface IRoleService
    {
        Task<List<RoleListViewModel>> GetAllAsync();

        Task<IdentityRole?> GetByIdAsync(
            string id);

        Task<bool> ExistsAsync(
            string name);

        Task<IdentityResult> CreateAsync(
            string name);

        Task<EditRoleViewModel?> GetEditModelAsync(
            string id);

        Task<IdentityResult> UpdateAsync(
            EditRoleViewModel model);

        Task<List<PermissionGroupViewModel>>
            GetPermissionGroupsAsync(
                string roleId);
    }
}