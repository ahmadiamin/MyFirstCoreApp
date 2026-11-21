using MyFirstCoreApp.Application.Security;

namespace MyFirstCoreApp.Application.Interfaces;

public interface ILoginCapacityService
{
    /// <summary>شرکت‌های فعالِ کاربر به تفکیک سمت (مالک / کارمند).</summary>
    Task<LoginCapacityInfo> GetAsync(string userId);
}

/// <summary>
/// سمتی که کاربر «در همین نشست» انتخاب کرده. فقط وقتی لازم است که کاربر هر دو سمت را دارد.
/// مقدار به «کاربر» وابسته است: با ورودِ کاربر دیگر در همان مرورگر، انتخاب قبلی بی‌اثر می‌شود.
/// </summary>
public interface ILoginCapacityAccessor
{
    LoginCapacity? GetChosen(string userId);
    void Set(string userId, LoginCapacity capacity);
    void Clear();
}
