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

    /// <summary>
    /// لحظه‌ی UTC ذخیره‌شده (مثل SentDate یا History.Date) را به ساعت محلی تبدیل می‌کند.
    /// فقط برای نمایش به کاربر.
    /// </summary>
    public static DateTime ToLocalTime(this TimeProvider timeProvider, DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            timeProvider.LocalTimeZone);

    /// <summary>روز محلی یک لحظه‌ی UTC (برای نمایش تاریخ شمسی رویدادها).</summary>
    public static DateOnly ToLocalDate(this TimeProvider timeProvider, DateTime utc)
        => DateOnly.FromDateTime(timeProvider.ToLocalTime(utc));
}
