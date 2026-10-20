using MyFirstCoreApp.Application.Common.Interfaces;

namespace MyFirstCoreApp.Services.CurrentUser;

public class CurrentTenantService : ICurrentTenant
{
    public const string TenantIdClaimType = "TenantId";
    public const string TenantNameClaimType = "TenantName";
    public const string SessionKeyTenantId = "CurrentTenantId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // 1) از Claim بخون (وقتی کاربر لاگین کرده و Tenant انتخاب شده)
            var claim = httpContext.User?.FindFirst(TenantIdClaimType)?.Value;
            if (int.TryParse(claim, out var claimTenantId))
                return claimTenantId;

            // 2) از Session بخون
            var sessionValue = httpContext.Session?.GetInt32(SessionKeyTenantId);
            return sessionValue;
        }
    }

    public string? TenantName =>
        _httpContextAccessor.HttpContext?.User?
            .FindFirst(TenantNameClaimType)?.Value;

    public bool IsSet => TenantId.HasValue;
}