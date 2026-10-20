using MyFirstCoreApp.Application.Banking;
using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// قواعد حسابداری چک (مدل «ثبت هنگام دریافت/صدور»):
///
///   دریافتنی:  ثبت چک    ← حساب چک‌های دریافتنی (بدهکار) / دریافتنی-طرف‌حساب (بستانکار)
///              پاس/نقد   ← بانک (بدهکار) / حساب چک‌های دریافتنی (بستانکار)
///              برگشت/ابطال از حالت در جریان ← سند معکوس ثبت
///   پرداختنی:  آینه‌ی همین‌ها (پرداختنی-طرف‌حساب بدهکار / حساب چک‌های پرداختنی بستانکار، ...)
///
/// همه‌ی متدها در صورت نقض قاعده InvalidOperationException با پیام فارسی می‌دهند و
/// هر عملیات (وضعیت + سند + تاریخچه) داخل یک تراکنش دیتابیس است.
/// </summary>
public interface IChequeService
{
    // ============================================
    // CRUD
    // ============================================

    /// <summary>ثبت چک «در جریان» + سند ثبت چک (به تاریخ صدور).</summary>
    Task<Cheque> CreateAsync(ChequeRequest request, int tenantId);

    /// <summary>
    /// ویرایش چک. جهت چک قابل تغییر نیست. برای چک در انتظار، سند ثبت با مبلغ/طرف حساب جدید بازسازی می‌شود.
    /// </summary>
    Task UpdateAsync(int chequeId, ChequeRequest request, int tenantId);

    /// <summary>حذف نرم چک «در جریان» همراه سندهایش.</summary>
    Task DeleteAsync(int chequeId, int tenantId);

    // ============================================
    // گردش کار
    // ============================================

    /// <summary>به بانک داده شد (فقط تاریخچه؛ سند ندارد).</summary>
    Task MarkAsDepositedAsync(int chequeId, int tenantId, string? description);

    /// <summary>پاس شد: سند بانک ↔ حساب چک به تاریخ پاس (پیش‌فرض: امروز ایران).</summary>
    Task MarkAsClearedAsync(int chequeId, int tenantId, DateOnly? clearedDate, string? bankTrackingNumber);

    /// <summary>برگشت خورد: سند ثبت چک معکوس می‌شود (مانده‌ی طرف حساب برمی‌گردد).</summary>
    Task MarkAsBouncedAsync(int chequeId, int tenantId, string? bounceReason, DateOnly? bouncedDate);

    /// <summary>عودت داده شد (فقط از برگشتی؛ سند ندارد چون در برگشت معکوس شده).</summary>
    Task MarkAsReturnedAsync(int chequeId, int tenantId, string? description);

    /// <summary>مجدداً ارائه شد: سررسید جدید + سند ثبت چک دوباره.</summary>
    Task MarkAsRePresentedAsync(int chequeId, int tenantId, DateOnly newDueDate, string? description);

    /// <summary>ابطال شد: اگر چک در انتظار بود سند ثبت معکوس می‌شود.</summary>
    Task MarkAsCancelledAsync(int chequeId, int tenantId, string? reason);

    /// <summary>ارجاع به صاحب چک (فقط از برگشتی؛ سند ندارد).</summary>
    Task MarkAsReferredAsync(int chequeId, int tenantId, string? description);

    /// <summary>نقد شد: مانند پاس شدن (سند بانک ↔ حساب چک).</summary>
    Task MarkAsCashedAsync(int chequeId, int tenantId, DateOnly? cashedDate, string? description);

    /// <summary>ثبت یادداشت.</summary>
    Task AddNoteAsync(int chequeId, int tenantId, string note);

    /// <summary>برگشت از پاس (اشتباه پاس شده): سند پاس حذف می‌شود و چک دوباره «در جریان» است.</summary>
    Task UnclearAsync(int chequeId, int tenantId, string? reason);

    /// <summary>برگشت از نقد (اشتباه نقد شده).</summary>
    Task UncashAsync(int chequeId, int tenantId, string? reason);
}
