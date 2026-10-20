using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// مدیریت سال‌های مالی. همه‌ی متدها در صورت نقض قاعده InvalidOperationException با پیام فارسی می‌دهند.
/// (ثبت/ویرایش/حذف «سند» در سال بسته در TransactionService کنترل می‌شود، نه اینجا.)
/// </summary>
public interface IFiscalYearService
{
    /// <summary>
    /// ساخت سال مالی. اگر نام خالی باشد، «سال مالی {سال شمسی شروع}» گذاشته می‌شود.
    /// همپوشانی با سال‌های دیگر شرکت و طول بیش از ۴۰۰ روز رد می‌شود.
    /// </summary>
    Task<FiscalYear> CreateAsync(FiscalYearRequest request, int tenantId);

    /// <summary>
    /// ویرایش نام و بازه (فقط سال باز). هیچ سند موجودی نباید از بازه‌ی جدید بیرون بیفتد.
    /// </summary>
    Task UpdateAsync(int fiscalYearId, FiscalYearRequest request, int tenantId);

    /// <summary>
    /// بستن سال مالی. اگر سند پیش‌نویس (قطعی‌نشده) دارد رد می‌شود.
    /// </summary>
    Task CloseAsync(int fiscalYearId, int tenantId);

    /// <summary>
    /// باز کردن مجدد. تا وقتی سال بعدی بسته است مجاز نیست (ترتیب منطقی سال‌ها حفظ شود).
    /// </summary>
    Task ReopenAsync(int fiscalYearId, int tenantId);

    /// <summary>حذف نرم سال مالیِ بدون سند.</summary>
    Task DeleteAsync(int fiscalYearId, int tenantId);

    /// <summary>سالی که امروز (به وقت ایران) در آن است؛ اگر نبود، آخرین سال تعریف‌شده؛ اگر هیچ سالی نبود null.</summary>
    Task<FiscalYear?> GetCurrentAsync(int tenantId);
}
