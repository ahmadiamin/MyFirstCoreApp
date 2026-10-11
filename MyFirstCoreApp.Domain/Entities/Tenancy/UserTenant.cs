using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Tenancy;

/// <summary>
/// ارتباط چند-به-چند بین User و Tenant
/// (یک کاربر می‌تونه توی چند شرکت عضو باشه)
/// </summary>
public class UserTenant : BaseEntity
{
    public string UserId { get; set; } = string.Empty;    // FK به AspNetUsers
    public int TenantId { get; set; }                     // FK به Tenants

    public bool IsOwner { get; set; } = false;            // آیا مالک این Tenant هست؟
    public bool IsDefault { get; set; } = false;          // Tenant پیش‌فرض کاربر
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}