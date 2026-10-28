using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Security.Claims;

namespace MyFirstCoreApp.Authorization
{
    public class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ApplicationDbContext _context;

        public PermissionAuthorizationHandler(
            ApplicationDbContext context)
        {
            _context = context;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return;

            if (context.User.IsInRole("SuperAdmin"))
            {
                context.Succeed(requirement);
                return;
            }

            var userId = context.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return;

            var hasPermission = await _context.UserRoles
                .Where(x => x.UserId == userId)
                .Join(
                    _context.RolePermissions,
                    userRole => userRole.RoleId,
                    rolePermission => rolePermission.RoleId,
                    (userRole, rolePermission) => rolePermission.PermissionId)
                .Join(
                    _context.Permissions,
                    permissionId => permissionId,
                    permission => permission.Id,
                    (permissionId, permission) => permission)
                .AnyAsync(x =>
                    x.Name == requirement.Permission &&
                    x.IsActive);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
        }
    }
}
