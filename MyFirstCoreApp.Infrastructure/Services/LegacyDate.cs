namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// پل موقت: تا زمانی که Invoice و Cheque به DateOnly تبدیل نشده‌اند (مراحل 1E و 1F)،
/// تاریخ DateTime آن‌ها را به تاریخ روز ایران تبدیل می‌کند.
///
/// فرمول همان فرمول Migration است (+۳:۳۰):
///  - مقدار ساعت صفر (انتخاب‌شده با Datepicker) همان روز می‌ماند.
///  - مقدار UtcNow به تاریخ درست ایران تبدیل می‌شود.
///
/// این کلاس با تمام شدن مرحله‌ی 1F حذف می‌شود.
/// </summary>
public static class LegacyDate
{
    private static readonly TimeSpan IranOffset = TimeSpan.FromMinutes(210);

    public static DateOnly ToDateOnly(DateTime value)
        => DateOnly.FromDateTime(value + IranOffset);
}
