namespace MyFirstCoreApp.Domain.Enums;

/// <summary>
/// وضعیت فاکتور
/// </summary>
public enum InvoiceStatus
{
    // ============================================
    // 🆕 پیش‌فاکتور
    // ============================================

    /// <summary>پیش‌نویس (داخلی)</summary>
    Draft = 1,

    /// <summary>ارسال شده به مشتری</summary>
    Sent = 2,

    /// <summary>اصلاح شده (دوباره باید ارسال بشه)</summary>
    Revised = 3,

    /// <summary>تایید شده توسط مشتری</summary>
    Approved = 4,

    /// <summary>رد شده توسط مشتری</summary>
    Rejected = 5,

    /// <summary>به تعویق افتاده (تاریخ پیگیری)</summary>
    Postponed = 6,

    /// <summary>تبدیل شده به فاکتور</summary>
    Converted = 7,

    /// <summary>لغو شده (داخلی)</summary>
    Cancelled = 8,

    // ============================================
    // 🆕 فاکتور (فروش/خرید)
    // ============================================

    /// <summary>قطعی</summary>
    Posted = 9
}