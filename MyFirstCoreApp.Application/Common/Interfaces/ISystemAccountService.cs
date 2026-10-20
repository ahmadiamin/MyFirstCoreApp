using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// حساب‌های سیستمی (بانک، دریافتنی، پرداختنی، ...) که اسناد خودکار به آن‌ها نیاز دارند.
/// قبلاً کد «پیدا کن یا بساز» در چند سرویس کپی شده بود.
///
/// توجه: فعلاً حساب با «نام + نوع» پیدا می‌شود. اگر کاربر نام حساب را عوض کند، حساب تکراری
/// ساخته می‌شود. اصلاح اصلی (کلید ثابت SystemAccountKey روی Account) یک Migration جدا می‌خواهد
/// و در مرحله‌ی بعد انجام می‌شود؛ چون اینجا تمرکز شد، تغییر آن فقط در یک کلاس خواهد بود.
/// </summary>
public interface ISystemAccountService
{
    Task<Account> GetOrCreateAsync(int tenantId, string name, AccountType type);
}
