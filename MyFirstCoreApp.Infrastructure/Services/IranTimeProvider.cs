namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// TimeProvider استاندارد .NET با منطقه‌ی زمانی ایران.
/// نتیجه: GetLocalNow() همیشه ساعت ایران را می‌دهد، مستقل از تنظیم ساعت سرور میزبان.
/// ثبت در DI:  services.AddSingleton&lt;TimeProvider, IranTimeProvider&gt;();
/// </summary>
public class IranTimeProvider : TimeProvider
{
    private static readonly TimeZoneInfo IranZone = ResolveZone();

    public override TimeZoneInfo LocalTimeZone => IranZone;

    private static TimeZoneInfo ResolveZone()
    {
        // ویندوز: "Iran Standard Time" — لینوکس/ماک: "Asia/Tehran"
        foreach (var id in new[] { "Iran Standard Time", "Asia/Tehran" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // آخرین راه: ایران از ۱۴۰۱ ساعت تابستانی ندارد؛ UTC+03:30 ثابت
        return TimeZoneInfo.CreateCustomTimeZone(
            id: "Iran",
            baseUtcOffset: TimeSpan.FromMinutes(210),
            displayName: "Iran",
            standardDisplayName: "Iran");
    }
}
