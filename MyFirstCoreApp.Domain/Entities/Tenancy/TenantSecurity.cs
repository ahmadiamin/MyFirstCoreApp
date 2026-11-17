using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Tenancy;

/// <summary>
/// نقشِ مخصوص «یک شرکت» (مثلاً «کارشناس فروش»). نقش‌های سراسری سیستم (Identity) جدا می‌مانند
/// و فقط برای مدیر سیستم‌اند؛ شرکت‌ها هرگز نقش سراسری نمی‌سازند و نمی‌بینند.
/// </summary>
public class TenantRole : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TenantRolePermission> Permissions { get; set; } = new List<TenantRolePermission>();
}

/// <summary>
/// یک دسترسی از کاتالوگ سیستم که به نقشِ شرکت داده شده.
/// دسترسی با «نام» (مثل Invoice.View) نگه داشته می‌شود؛ نام پایدارترین کلید کاتالوگ است
/// و هنگام استفاده همیشه با کاتالوگ فعالِ سیستم تطبیق داده می‌شود.
/// </summary>
public class TenantRolePermission : TenantEntity
{
    public int TenantRoleId { get; set; }
    public TenantRole TenantRole { get; set; } = null!;

    public string PermissionName { get; set; } = string.Empty;
}

/// <summary>نقشی که یک کاربر «در یک شرکت» دارد.</summary>
public class UserTenantRole : TenantEntity
{
    public string UserId { get; set; } = string.Empty;

    public int TenantRoleId { get; set; }
    public TenantRole TenantRole { get; set; } = null!;
}
