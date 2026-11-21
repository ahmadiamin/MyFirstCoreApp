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

/// <summary>افزودن فرد جدید به شرکت (شامل ساخت حساب با موبایل) و ارسال کد تعیین رمز با پیامک.</summary>
public interface ITenantStaffService
{
    /// <summary>
    /// با «موبایل»:
    ///  - حسابی با این موبایل «نیست»: حساب جدید ساخته می‌شود (رمزِ تصادفیِ ناشناخته)، موبایل تأییدشده‌ی مالک-تأیید
    ///    علامت می‌خورد و کد تعیین رمز پیامک می‌شود؛ کارمند با آن خودش رمز می‌گذارد.
    ///  - حسابِ «تأییدشده» با این موبایل هست (مثلاً مدیر شرکت دیگر): همان حساب (بدون تغییر رمز و مشخصات حساب) اضافه می‌شود.
    ///  - حسابی با این موبایل هست ولی «تأییدنشده» است: رد می‌شود (جلوگیری از تصاحب شماره).
    /// همه‌چیز (به‌جز پیامک) در یک تراکنش است؛ پیامک بعد از Commit فرستاده می‌شود.
    /// </summary>
    Task<MemberCreateResult> AddMemberAsync(int tenantId, MemberCreateRequest request);

    /// <summary>
    /// ارسال دوباره‌ی کد تعیین رمز به موبایل یکی از اعضای «همین شرکت». عضو نبودن، غیرفعال بودن یا نداشتن
    /// موبایل تأییدشده خطای فارسی می‌دهد. محدودیت تعداد ارسال را خودِ سرویس OTP اعمال می‌کند.
    /// </summary>
    Task<StaffPasswordCodeResult> SendPasswordCodeAsync(int tenantId, string userId);
}
