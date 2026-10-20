using MyFirstCoreApp.Infrastructure.Services;

namespace MyFirstCoreApp.Tests.TestSupport;

/// <summary>
/// ساعت ثابت با منطقه‌ی زمانی ایران. «امروز» در تست‌ها قابل پیش‌بینی می‌شود.
/// </summary>
public sealed class FixedTimeProvider : IranTimeProvider
{
    private DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
