using MyFirstCoreApp.Domain.Entities.Identity;
using System.Security.Claims;

namespace MyFirstCoreApp.Services.Interfaces
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permission);

        Task<List<Permission>> GetAllAsync();

        Task<Permission?> GetByIdAsync(int id);

        Task<bool> ExistsAsync(
            string name,
            int? excludeId = null);

        Task CreateAsync(
            Permission permission);

        Task UpdateAsync(
            Permission permission);
    }
}