using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// درخت منوی «قابل نمایش برای یک کاربر در شرکت فعالش».
///
///  - منوی بدون دسترسی: برای هر کاربر واردشده.
///  - منوی دارای دسترسی: فقط اگر IPermissionEvaluator اجازه بدهد
///    (سطح شرکت ← فقط از سمت‌های همان شرکت یا مالک؛ سراسری ← فقط مدیر سیستم؛ سطح حساب ← همه).
///  - گروهی که هیچ فرزند قابل‌نمایش ندارد و خودش آدرس ندارد حذف می‌شود.
///  - کاربر ناشناس: هیچ.
/// </summary>
public interface IMenuTreeService
{
    Task<List<MenuNode>> GetVisibleTreeAsync(PermissionSubject subject);
}
