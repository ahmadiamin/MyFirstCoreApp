using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Infrastructure.Security;

public class PermissionEvaluator : IPermissionEvaluator
{
    private readonly ITenantAccessService _tenantAccess;
    private readonly IPlatformPermissionChecker _platform;

    public PermissionEvaluator(ITenantAccessService tenantAccess, IPlatformPermissionChecker platform)
    {
        _tenantAccess = tenantAccess;
        _platform = platform;
    }

    public async Task<bool> HasAsync(PermissionSubject subject, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return subject.IsAuthenticated;

        var allowed = await GetAllowedAsync(subject, new[] { permission });
        return allowed.Contains(permission);
    }

    public async Task<IReadOnlySet<string>> GetAllowedAsync(PermissionSubject subject, IEnumerable<string?> permissions)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!subject.IsAuthenticated || string.IsNullOrEmpty(subject.UserId))
            return result;

        var requested = permissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // مدیر سیستم
        if (subject.IsSuperAdmin)
        {
            result.UnionWith(requested);
            return result;
        }

        IReadOnlySet<string>? platformSet = null;
        IReadOnlySet<string>? tenantSet = null;

        foreach (var permission in requested)
        {
            if (PermissionCatalog.IsAccountLevel(permission))
            {
                // دامنه‌اش را عضویتِ خودِ کاربر محدود می‌کند (TenantService فقط شرکت‌های همین کاربر را می‌دهد)
                result.Add(permission);
            }
            else if (PermissionCatalog.IsPlatform(permission))
            {
                platformSet ??= await _platform.GetPermissionsAsync(subject.UserId);

                if (platformSet.Contains(permission))
                    result.Add(permission);
            }
            else if (subject.TenantId is int tenantId)
            {
                // سطح شرکت: فقط با شرکت فعال؛ بدون شرکت فعال هیچ دسترسی شرکتی نیست
                tenantSet ??= await _tenantAccess.GetPermissionsAsync(subject.UserId, tenantId);

                if (tenantSet.Contains(permission))
                    result.Add(permission);
            }
        }

        return result;
    }
}
