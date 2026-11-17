using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// نقطه‌ی واحد تصمیم‌گیری دسترسی (Handler سیاست‌ها، منوها، داشبورد، ... همه از اینجا می‌پرسند):
///   وارد‌نشده ← رد
///   مدیر سیستم (SuperAdmin) ← قبول
///   سطح حساب (Tenancy.View/Create/Select) ← قبول (دامنه را عضویت محدود می‌کند)
///   سراسری (Permission.*, Menu.*) ← فقط از راه نقش‌های سراسریِ Identity
///   سطح شرکت ← فقط با شرکت فعال و عضویت + (مالک یا نقشِ دارای آن دسترسی در همان شرکت)
/// </summary>
public interface IPermissionEvaluator
{
    Task<bool> HasAsync(PermissionSubject subject, string permission);

    /// <summary>
    /// زیرمجموعه‌ی «مجاز» از فهرست داده‌شده (مثلاً برای منو). مقدار خالی/null در نتیجه نمی‌آید؛
    /// فراخواننده آن را «بدون نیاز به دسترسی» (مجاز برای هر کاربر واردشده) حساب می‌کند.
    /// </summary>
    Task<IReadOnlySet<string>> GetAllowedAsync(PermissionSubject subject, IEnumerable<string?> permissions);
}

/// <summary>دسترسی‌های سراسریِ یک کاربر از راه نقش‌های Identity (فقط برای مدیران سیستم).</summary>
public interface IPlatformPermissionChecker
{
    Task<IReadOnlySet<string>> GetPermissionsAsync(string userId);
}
