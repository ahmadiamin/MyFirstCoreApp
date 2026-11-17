using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// مدیریت «سمت‌ها (نقش‌های شرکت)» و «افراد» یک شرکت. شناسه‌ی شرکت را همیشه از شرکت فعال بده
/// (ICurrentTenant)، نه از ورودی کاربر. قاعده‌ی نقض‌شده با InvalidOperationException و پیام فارسی اعلام می‌شود.
///
/// قواعد مهم:
///  - نقش شرکت فقط دسترسی «سطح شرکت» می‌گیرد؛ دسترسی سراسری (Permission.*, Menu.*) رد می‌شود.
///  - فقط عضوِ همین شرکت دیده/ویرایش می‌شود (کاربر شرکت دیگر «پیدا نشد»).
///  - مالک شرکت حذف نمی‌شود و نقش نمی‌گیرد (همه‌ی دسترسی‌های شرکت را دارد).
/// </summary>
public interface ITenantSecurityService
{
    // ---------- سمت‌ها (نقش‌ها) ----------

    /// <summary>دسترسی‌های قابل‌اختصاص به نقش شرکت (فعال، سطح شرکت)، گروه‌بندی‌شده.</summary>
    Task<IReadOnlyList<PermissionGroupInfo>> GetAssignablePermissionsAsync();

    Task<IReadOnlyList<TenantRoleSummary>> GetRolesAsync(int tenantId);
    Task<TenantRoleDetails?> GetRoleAsync(int tenantId, int roleId);
    Task<int> CreateRoleAsync(int tenantId, TenantRoleRequest request);
    Task UpdateRoleAsync(int tenantId, int roleId, TenantRoleRequest request);

    /// <summary>حذف نقش؛ نقش از همه‌ی افراد هم برداشته می‌شود (افراد حذف نمی‌شوند).</summary>
    Task DeleteRoleAsync(int tenantId, int roleId);

    /// <summary>
    /// آیا این دسترسی در سمتِ «هر شرکتی» استفاده شده؟ (مدیریت کاتالوگ سراسری؛ فقط مدیر سیستم.)
    /// برای جلوگیری از تغییر نامِ دسترسیِ در‌حال‌استفاده؛ چون سمت‌ها دسترسی را با نام نگه می‌دارند.
    /// </summary>
    Task<bool> IsPermissionInUseAsync(string permissionName);

    // ---------- افراد ----------

    Task<IReadOnlyList<TenantMemberInfo>> GetMembersAsync(int tenantId);
    Task<TenantMemberInfo?> GetMemberAsync(int tenantId, string userId);

    /// <summary>نام، فامیل، عنوان شغلی و تلفن داخلی. تلفن داخلی در هر شرکت یکتاست.</summary>
    Task UpdateMemberProfileAsync(int tenantId, string userId, MemberProfileRequest request);

    /// <summary>نقش‌های فرد در این شرکت را «جایگزین» می‌کند.</summary>
    Task SetMemberRolesAsync(int tenantId, string userId, IReadOnlyCollection<int> roleIds);

    /// <summary>عضویت را حذف می‌کند (حساب کاربری سر جایش می‌ماند). مالک حذف نمی‌شود.</summary>
    Task RemoveMemberAsync(int tenantId, string userId);
}

/// <summary>افزودن فرد جدید به شرکت (شامل ساخت حساب کاربری).</summary>
public interface ITenantStaffService
{
    /// <summary>
    /// اگر کاربری با این ایمیل «نیست»، حساب جدید با رمز داده‌شده ساخته می‌شود؛
    /// اگر هست، همان کاربر (بدون تغییر رمز و نام حساب) به این شرکت اضافه می‌شود.
    /// </summary>
    Task<MemberCreateResult> AddMemberAsync(int tenantId, MemberCreateRequest request);
}
