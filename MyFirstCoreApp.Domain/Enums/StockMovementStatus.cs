namespace MyFirstCoreApp.Domain.Enums;

/// <summary>
/// وضعیت سند گردش انبار
/// </summary>
public enum StockMovementStatus
{
    /// <summary>پیش‌نویس؛ در موجودی محاسبه نمی‌شود</summary>
    Draft = 1,

    /// <summary>ثبت قطعی؛ در موجودی محاسبه می‌شود</summary>
    Posted = 2,

    /// <summary>لغوشده؛ در موجودی محاسبه نمی‌شود</summary>
    Cancelled = 3
}