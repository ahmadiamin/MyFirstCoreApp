using System.ComponentModel.DataAnnotations;
using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Areas.Company.ViewModels;

// ============================================
// سمت‌ها (نقش‌های شرکت)
// ============================================

public class PositionFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "نام سمت")]
    [Required(ErrorMessage = "نام سمت الزامی است.")]
    [StringLength(100, ErrorMessage = "نام سمت حداکثر ۱۰۰ کاراکتر است.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "توضیحات")]
    [StringLength(300, ErrorMessage = "توضیحات حداکثر ۳۰۰ کاراکتر است.")]
    public string? Description { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    /// <summary>نام دسترسی‌های انتخاب‌شده (Invoice.View, ...).</summary>
    public List<string> SelectedPermissions { get; set; } = new();

    /// <summary>کاتالوگِ قابل‌اختصاص، گروه‌بندی‌شده (فقط برای نمایش؛ از سرویس پر می‌شود).</summary>
    public IReadOnlyList<PermissionGroupInfo> Groups { get; set; } = Array.Empty<PermissionGroupInfo>();
}

// ============================================
// افراد (کارکنان)
// ============================================

public class StaffCreateViewModel
{
    [Display(Name = "موبایل (شناسه‌ی ورود)")]
    [Required(ErrorMessage = "موبایل الزامی است.")]
    [RegularExpression(@"^(09|۰۹|٠٩)[0-9۰-۹٠-٩]{9}$", ErrorMessage = "موبایل باید ۱۱ رقم و با 09 شروع شود.")]
    public string Mobile { get; set; } = string.Empty;

    [Display(Name = "نام")]
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "نام خانوادگی")]
    [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "عنوان شغلی")]
    [StringLength(100)]
    public string? JobTitle { get; set; }

    [Display(Name = "تلفن داخلی")]
    [StringLength(20)]
    public string? InternalPhone { get; set; }

    public List<int> SelectedRoleIds { get; set; } = new();
    public IReadOnlyList<TenantRoleSummary> RoleOptions { get; set; } = Array.Empty<TenantRoleSummary>();
}

public class StaffEditViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsOwner { get; set; }

    [Display(Name = "نام")]
    [Required(ErrorMessage = "نام الزامی است.")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "نام خانوادگی")]
    [Required(ErrorMessage = "نام خانوادگی الزامی است.")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "عنوان شغلی")]
    [StringLength(100)]
    public string? JobTitle { get; set; }

    [Display(Name = "تلفن داخلی")]
    [StringLength(20)]
    public string? InternalPhone { get; set; }

    public List<int> SelectedRoleIds { get; set; } = new();
    public IReadOnlyList<TenantRoleSummary> RoleOptions { get; set; } = Array.Empty<TenantRoleSummary>();
}

/// <summary>انتخاب سمت‌ها برای یک فرد (بخش مشترک فرم‌های افزودن و ویرایش).</summary>
public class RolePickerViewModel
{
    public IReadOnlyList<TenantRoleSummary> RoleOptions { get; set; } = Array.Empty<TenantRoleSummary>();
    public List<int> SelectedRoleIds { get; set; } = new();
}
