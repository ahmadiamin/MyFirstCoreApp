namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// «این کاربر در این شرکت چه دسترسی‌هایی دارد؟» — تنها منبع حقیقتِ دسترسی سطح شرکت.
/// عضویت و نقش‌ها هر بار از دیتابیس سنجیده می‌شود (نتیجه فقط در همان درخواست حافظه می‌شود)،
/// پس حذف عضویت یا نقش بلافاصله اثر دارد.
/// </summary>
public interface ITenantAccessService
{
    Task<bool> IsMemberAsync(string userId, int tenantId);

    /// <summary>
    /// مجموعه‌ی دسترسی‌های «سطح شرکت»ی که کاربر در این شرکت دارد:
    ///  - مالک: همه‌ی دسترسی‌های فعالِ سطح شرکت
    ///  - عضو معمولی: اجتماع دسترسی‌های نقش‌های فعالش در همین شرکت
    ///  - غیرعضو، کاربر غیرفعال یا شرکت غیرفعال/حذف‌شده: هیچ
    /// دسترسی سراسری یا غیرفعال هیچ‌وقت در نتیجه نیست.
    /// </summary>
    Task<IReadOnlySet<string>> GetPermissionsAsync(string userId, int tenantId);

    Task<bool> HasPermissionAsync(string userId, int tenantId, string permission);
}
