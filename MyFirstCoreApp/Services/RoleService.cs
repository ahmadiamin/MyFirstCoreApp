using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Models.ViewModels;
using MyFirstCoreApp.Services.Interfaces;

namespace MyFirstCoreApp.Services
{
    public class RoleService: IRoleService
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public RoleService(
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<List<RoleListViewModel>> GetAllAsync()
        {
            return await _roleManager.Roles
                .OrderBy(x => x.Name)
                .Select(role => new RoleListViewModel
                {
                    Id = role.Id,
                    Name = role.Name ?? string.Empty,

                    UserCount = _context.UserRoles
                        .Count(userRole =>
                            userRole.RoleId == role.Id),

                    PermissionCount = _context.RolePermissions
                        .Count(rolePermission =>
                            rolePermission.RoleId == role.Id)
                })
                .ToListAsync();
        }

        public async Task<IdentityRole?> GetByIdAsync(string id)
        {
            return await _roleManager.FindByIdAsync(id);
        }

        public async Task<bool> ExistsAsync(string name)
        {
            return await _roleManager.RoleExistsAsync(name);
        }

        public async Task<IdentityResult> CreateAsync(string name)
        {
            var role = new IdentityRole
            {
                Name = name.Trim()
            };

            return await _roleManager.CreateAsync(role);
        }

        public async Task<EditRoleViewModel?> GetEditModelAsync(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);

            if (role == null)
                return null;

            var rolePermissionIds = await _context.RolePermissions
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.PermissionId)
                .ToListAsync();

            var permissions = await _context.Permissions
                .Where(x => x.IsActive)
                .OrderBy(x => x.GroupName)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return new EditRoleViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,

                PermissionGroups = BuildPermissionGroups(
                    permissions,
                    rolePermissionIds)
            };
        }

        public async Task<IdentityResult> UpdateAsync(
            EditRoleViewModel model)
        {
            var role = await _roleManager.FindByIdAsync(model.Id);

            if (role == null)
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Description = "نقش پیدا نشد."
                    });

            var normalizedName = model.Name.Trim();

            var existingRole =
                await _roleManager.FindByNameAsync(normalizedName);

            if (existingRole != null &&
                existingRole.Id != role.Id)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Description =
                            "این نام نقش قبلاً استفاده شده است."
                    });
            }

            role.Name = normalizedName;

            var roleResult =
                await _roleManager.UpdateAsync(role);

            if (!roleResult.Succeeded)
                return roleResult;

            var selectedPermissionIds =
                model.PermissionGroups
                    .SelectMany(x => x.Permissions)
                    .Where(x => x.Selected)
                    .Select(x => x.Id)
                    .Distinct()
                    .ToList();

            var currentPermissionIds =
                await _context.RolePermissions
                    .Where(x => x.RoleId == role.Id)
                    .Select(x => x.PermissionId)
                    .ToListAsync();

            var permissionsToRemove =
                currentPermissionIds
                    .Except(selectedPermissionIds)
                    .ToList();

            if (permissionsToRemove.Count > 0)
            {
                var itemsToRemove =
                    await _context.RolePermissions
                        .Where(x =>
                            x.RoleId == role.Id &&
                            permissionsToRemove.Contains(
                                x.PermissionId))
                        .ToListAsync();

                _context.RolePermissions.RemoveRange(
                    itemsToRemove);
            }

            var permissionsToAdd =
                selectedPermissionIds
                    .Except(currentPermissionIds)
                    .ToList();

            foreach (var permissionId in permissionsToAdd)
            {
                _context.RolePermissions.Add(
                    new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionId
                    });
            }

            await _context.SaveChangesAsync();

            return IdentityResult.Success;
        }

        public async Task<List<PermissionGroupViewModel>>
            GetPermissionGroupsAsync(string roleId)
        {
            var selectedPermissionIds =
                await _context.RolePermissions
                    .Where(x => x.RoleId == roleId)
                    .Select(x => x.PermissionId)
                    .ToListAsync();

            var permissions =
                await _context.Permissions
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.GroupName)
                    .ThenBy(x => x.Name)
                    .ToListAsync();

            return BuildPermissionGroups(
                permissions,
                selectedPermissionIds);
        }

        private static List<PermissionGroupViewModel>
            BuildPermissionGroups(
                List<Permission> permissions,
                List<int> selectedPermissionIds)
        {
            return permissions
                .GroupBy(x => x.GroupName ?? "سایر")
                .Select(group =>
                    new PermissionGroupViewModel
                    {
                        GroupName = group.Key,

                        Permissions = group
                            .Select(permission =>
                                new PermissionItemViewModel
                                {
                                    Id = permission.Id,
                                    Name = permission.Name,
                                    DisplayName =
                                        permission.DisplayName,
                                    Selected =
                                        selectedPermissionIds
                                            .Contains(
                                                permission.Id)
                                })
                            .ToList()
                    })
                .ToList();
        }
    }
}