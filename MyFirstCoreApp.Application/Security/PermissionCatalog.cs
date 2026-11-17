namespace MyFirstCoreApp.Application.Security;

/// <summary>
/// طبقه‌بندی دسترسی‌های سیستم (در کد، نه دیتابیس؛ همراه نسخه‌ی برنامه و قابل تست):
///
///  سراسری (Platform)    : مدیریت کاتالوگ دسترسی‌ها و منوها. فقط مدیر سیستم (نقش‌های سراسری Identity).
///  سطح حساب (Account)   : دیدن/ساختن/انتخاب شرکت‌های «خودِ کاربر»؛ هر کاربر واردشده دارد
///                         (دامنه‌ی این صفحه‌ها را همان عضویت کاربر محدود می‌کند).
///  سطح شرکت (Tenant)    : بقیه (فاکتور، چک، گزارش، کاربران و نقش‌های شرکت، ...).
///                         مالک شرکت همه را دارد؛ دیگران فقط از راه نقش‌هایشان «در همان شرکت».
/// </summary>
public static class PermissionCatalog
{
    private static readonly string[] PlatformPrefixes = { "Permission.", "Menu." };

    private static readonly HashSet<string> AccountLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tenancy.View",
        "Tenancy.Create",
        "Tenancy.Select"
    };

    public static bool IsPlatform(string permission)
        => PlatformPrefixes.Any(prefix => permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public static bool IsAccountLevel(string permission)
        => AccountLevel.Contains(permission);

    /// <summary>دسترسی‌ای که می‌شود به «نقشِ شرکت» داد (نه سراسری، نه سطح حساب).</summary>
    public static bool IsTenantScope(string permission)
        => !IsPlatform(permission) && !IsAccountLevel(permission);
}
