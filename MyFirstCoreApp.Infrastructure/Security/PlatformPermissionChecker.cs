using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Security;

/// <summary>
/// دسترسی‌های «سراسری» از راه نقش‌های Identity (همان مسیر قدیمی)؛ فقط برای مدیران سیستم.
/// شرکت‌ها از این مسیر چیزی نمی‌گیرند: نقش‌های شرکت جدا و در جدول TenantRoles هستند.
/// </summary>
public class PlatformPermissionChecker : IPlatformPermissionChecker
{
    private readonly ApplicationDbContext _context;

    public PlatformPermissionChecker(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(string userId)
    {
        var names = await _context.UserRoles
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
            .Where(permission => permission.IsActive)
            .Select(permission => permission.Name)
            .Distinct()
            .ToListAsync();

        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }
}
