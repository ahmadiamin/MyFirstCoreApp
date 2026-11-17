using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Security;

public class TenantAccessService : ITenantAccessService
{
    private readonly ApplicationDbContext _context;

    // نتیجه فقط در همین درخواست (Scope) حافظه می‌شود؛ درخواست بعدی دوباره از دیتابیس می‌پرسد
    private readonly Dictionary<(string UserId, int TenantId), IReadOnlySet<string>> _cache = new();

    public TenantAccessService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> IsMemberAsync(string userId, int tenantId)
        => _context.UserTenants.AnyAsync(ut => ut.UserId == userId && ut.TenantId == tenantId && !ut.IsDeleted);

    public async Task<bool> HasPermissionAsync(string userId, int tenantId, string permission)
        => (await GetPermissionsAsync(userId, tenantId)).Contains(permission);

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(string userId, int tenantId)
    {
        if (string.IsNullOrEmpty(userId))
            return Empty();

        if (_cache.TryGetValue((userId, tenantId), out var cached))
            return cached;

        var result = await ComputeAsync(userId, tenantId);
        _cache[(userId, tenantId)] = result;
        return result;
    }

    private async Task<IReadOnlySet<string>> ComputeAsync(string userId, int tenantId)
    {
        // ۱) عضویت فعال (حذف‌نرم‌شده توسط فیلتر سراسری کنار می‌رود)
        var membership = await _context.UserTenants
            .AsNoTracking()
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId && !ut.IsDeleted);

        if (membership == null)
            return Empty();

        // ۲) شرکت و کاربر باید فعال باشند
        var tenantActive = await _context.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive && !t.IsDeleted);
        if (!tenantActive)
            return Empty();

        var userActive = await _context.Users.AnyAsync(u => u.Id == userId && u.IsActive);
        if (!userActive)
            return Empty();

        // ۳) کاتالوگ دسترسی‌های فعال (دسترسیِ غیرفعال‌شده یا حذف‌شده از کاتالوگ، فوراً بی‌اثر می‌شود)
        var catalog = (await _context.Permissions
                .Where(p => p.IsActive)
                .Select(p => p.Name)
                .ToListAsync())
            .Where(PermissionCatalog.IsTenantScope)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // ۴) مالک: همه‌ی دسترسی‌های سطح شرکت
        if (membership.IsOwner)
            return catalog;

        // ۵) عضو معمولی: اجتماع دسترسی‌های نقش‌های فعالش «در همین شرکت».
        //    صریحاً با IgnoreQueryFilters و TenantId نوشته شده تا به شرکتِ جاریِ Session وابسته نباشد.
        var granted = await (
                from assignment in _context.UserTenantRoles.IgnoreQueryFilters()
                join role in _context.TenantRoles.IgnoreQueryFilters() on assignment.TenantRoleId equals role.Id
                join permission in _context.TenantRolePermissions.IgnoreQueryFilters() on role.Id equals permission.TenantRoleId
                where assignment.TenantId == tenantId && assignment.UserId == userId && !assignment.IsDeleted
                      && role.TenantId == tenantId && !role.IsDeleted && role.IsActive
                      && permission.TenantId == tenantId && !permission.IsDeleted
                select permission.PermissionName)
            .Distinct()
            .ToListAsync();

        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in granted)
        {
            // فقط آنچه هنوز در کاتالوگِ فعال هست و «سطح شرکت» است؛ دسترسی سراسری هیچ‌وقت از این مسیر نمی‌آید
            if (catalog.Contains(name))
                effective.Add(name);
        }

        return effective;
    }

    private static IReadOnlySet<string> Empty() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
