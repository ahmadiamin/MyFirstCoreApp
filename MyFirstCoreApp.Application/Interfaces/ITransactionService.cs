using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// تمام قواعد سند حسابداری فقط اینجا اعمال می‌شود.
/// کنترلرها و سرویس‌های دیگر (فاکتور، پرداخت، چک) نباید مستقیم Transaction بسازند یا تغییر دهند.
/// همه‌ی متدها در صورت نقض قاعده‌ی کسب‌وکار InvalidOperationException با پیام فارسی می‌دهند.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// شماره‌ی پیشنهادی بعدی (فقط برای نمایش).
    /// شماره‌ی نهایی هنگام ثبت تعیین می‌شود (Number = 0 یعنی خودکار).
    /// </summary>
    Task<int> GetNextNumberAsync(int tenantId);

    /// <summary>
    /// ثبت سند جدید. اعتبارسنجی: تاریخ، حداقل ۲ خط، هر خط فقط بدهکار یا بستانکار،
    /// مبلغ صحیح، تراز بودن، معتبر بودن حساب/طرف‌حساب در شرکت جاری.
    /// </summary>
    Task<Transaction> CreateAsync(Transaction transaction);

    /// <summary>
    /// ویرایش سند دستی پیش‌نویس (هدر + جایگزینی کامل خطوط).
    /// سند قطعی یا خودکار قابل ویرایش نیست.
    /// </summary>
    Task UpdateAsync(Transaction transaction);

    /// <summary>قطعی کردن سند دستی.</summary>
    Task PostAsync(int id, int tenantId);

    /// <summary>برگرداندن سند دستی قطعی به پیش‌نویس.</summary>
    Task UnpostAsync(int id, int tenantId);

    /// <summary>
    /// حذف نرم هر سند (حتی خودکار/قطعی).
    /// فقط برای ماژول‌های فاکتور/پرداخت/چک هنگام Unpost؛ از UI صدا زده نشود.
    /// </summary>
    Task DeleteAsync(int id, int tenantId);

    /// <summary>
    /// حذف نرم سند دستی پیش‌نویس (مسیر UI).
    /// سند خودکار یا قطعی حذف نمی‌شود.
    /// </summary>
    Task DeleteManualAsync(int id, int tenantId);

    /// <summary>دریافت سند با خطوط.</summary>
    Task<Transaction?> GetByIdAsync(int id, int tenantId);
}
