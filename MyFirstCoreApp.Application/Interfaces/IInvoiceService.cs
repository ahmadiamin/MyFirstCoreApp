using MyFirstCoreApp.Application.Invoicing;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// همه‌ی متدها در صورت نقض قاعده‌ی کسب‌وکار InvalidOperationException با پیام فارسی می‌دهند.
/// عملیات چندمرحله‌ای (ثبت با ساخت سریع طرف‌حساب/کالا، قطعی، برگشت، تبدیل، تسویه)
/// داخل یک تراکنش دیتابیس اجرا می‌شوند.
/// </summary>
public interface IInvoiceService
{
    // ============================================
    // CRUD
    // ============================================

    /// <summary>شماره‌ی پیشنهادی (فقط نمایشی؛ شماره‌ی نهایی هنگام ثبت تعیین می‌شود).</summary>
    Task<int> GetNextNumberAsync(int tenantId, InvoiceType invoiceType);

    /// <summary>
    /// ثبت فاکتور (پیش‌نویس). مبالغ و شماره سمت سرویس محاسبه می‌شود.
    /// اگر طرف‌حساب/کالا «جدید» باشد، در همان تراکنش ساخته می‌شود؛ با خطا هیچ‌چیز باقی نمی‌ماند.
    /// </summary>
    Task<Invoice> CreateAsync(InvoiceRequest request, int tenantId);

    /// <summary>ویرایش فاکتور پیش‌نویس (یا پیش‌فاکتور اصلاح‌شده). نوع فاکتور قابل تغییر نیست.</summary>
    Task UpdateAsync(InvoiceRequest request, int tenantId);

    /// <summary>حذف نرم فاکتور پیش‌نویس.</summary>
    Task DeleteAsync(int id, int tenantId);

    // ============================================
    // Post / Unpost (فاکتور فروش/خرید)
    // ============================================

    Task PostAsync(int invoiceId, int tenantId);

    /// <summary>برگشت به پیش‌نویس. اگر پرداخت یا تخفیف تسویه دارد، رد می‌شود.</summary>
    Task UnpostAsync(int invoiceId, int tenantId);

    // ============================================
    // گردش کار پیش‌فاکتور
    // ============================================

    Task MarkAsSentAsync(int invoiceId, int tenantId);
    Task MarkAsRevisedAsync(int invoiceId, int tenantId, string? description);
    Task MarkAsApprovedAsync(int invoiceId, int tenantId, string? description);
    Task MarkAsRejectedAsync(int invoiceId, int tenantId, string? reason);
    Task MarkAsPostponedAsync(int invoiceId, int tenantId, DateOnly nextFollowUpDate, string? reason);
    Task MarkAsCancelledAsync(int invoiceId, int tenantId, string? reason);

    // ============================================
    // تبدیل و تسویه
    // ============================================

    /// <summary>تبدیل پیش‌فاکتور تاییدشده به فاکتور فروش (پیش‌نویس).</summary>
    Task<Invoice> ConvertToInvoiceAsync(int preInvoiceId, int tenantId);

    /// <summary>تسویه با تخفیف (سند جداگانه می‌سازد؛ سند اصلی فاکتور دست نمی‌خورد).</summary>
    Task SettleWithDiscountAsync(int invoiceId, int tenantId, decimal discountAmount);
}
