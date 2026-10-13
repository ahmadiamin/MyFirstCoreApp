namespace MyFirstCoreApp.Application.Common.Utilities;

public static class TimeProviderExtensions
{
    /// <summary>
    /// «امروز» به وقت محلی TimeProvider (در برنامه: ایران).
    /// برای مقدار پیش‌فرض تاریخ سند/فاکتور و بازه‌ی پیش‌فرض گزارش‌ها استفاده شود،
    /// نه DateTime.Now (وابسته به ساعت سرور) و نه DateTime.UtcNow.
    /// </summary>
    public static DateOnly Today(this TimeProvider timeProvider)
        => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
