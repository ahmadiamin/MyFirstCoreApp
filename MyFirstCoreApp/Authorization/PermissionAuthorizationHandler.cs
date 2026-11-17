using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Persistence;
using System.Security.Claims;

namespace MyFirstCoreApp.Authorization
{
    public class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionEvaluator _evaluator;
        private readonly ICurrentTenant _currentTenant;

        public PermissionAuthorizationHandler(
            IPermissionEvaluator evaluator,
            ICurrentTenant currentTenant,
            ApplicationDbContext context)
        {
            _evaluator = evaluator;
            _context = context;
            _currentTenant = currentTenant;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var subject = new PermissionSubject(
                IsAuthenticated: context.User.Identity?.IsAuthenticated == true,
                IsSuperAdmin: context.User.IsInRole("SuperAdmin"),
                UserId: context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                TenantId: _currentTenant.TenantId);

            if (await _evaluator.HasAsync(subject, requirement.Permission))
                context.Succeed(requirement);

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
