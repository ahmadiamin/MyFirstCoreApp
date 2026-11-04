namespace MyFirstCoreApp.Application.Common.Logging;

public enum RequestLogSeverity
{
    /// <summary>برای فایل‌های ثابت (CSS، JS، تصویر) که لاگ را شلوغ می‌کنند؛ در عمل ثبت نمی‌شود.</summary>
    Verbose,
    Information,
    Warning,
    Error
}

/// <summary>
/// سیاست «هر درخواست با چه سطحی لاگ شود» (تابع خالص؛ وابسته به Serilog یا ASP.NET نیست و قابل تست مستقیم).
///
///   خطا (Exception) یا 5xx            ← Error
///   401/403 (دسترسی رد شد)             ← Warning  (برای ردیابی تلاش‌های غیرمجاز)
///   درخواست کند (بیش از آستانه)         ← Warning  (فقط وقتی فایل ثابت نیست و خطا نداشته)
///   فایل ثابت موفق                     ← Verbose  (لاگ نمی‌شود)
///   بقیه (404، 400 و درخواست‌های عادی)   ← Information
/// </summary>
public static class RequestLogPolicy
{
    /// <summary>درخواستی که از این مقدار (میلی‌ثانیه) کندتر باشد Warning می‌گیرد.</summary>
    public const double SlowRequestThresholdMs = 3000;

    private static readonly string[] StaticPrefixes =
    {
        "/lib/", "/css/", "/js/", "/plugins/", "/dist/", "/images/", "/img/", "/fonts/", "/favicon", "/_framework/", "/_content/"
    };

    private static readonly string[] StaticExtensions =
    {
        ".css", ".js", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp",
        ".woff", ".woff2", ".ttf", ".eot", ".otf"
    };

    public static RequestLogSeverity Classify(int statusCode, bool hasException, string? path, double elapsedMs)
    {
        if (hasException || statusCode >= 500)
            return RequestLogSeverity.Error;

        if (statusCode is 401 or 403)
            return RequestLogSeverity.Warning;

        if (IsStaticAsset(path) && statusCode < 400)
            return RequestLogSeverity.Verbose;

        if (elapsedMs > SlowRequestThresholdMs)
            return RequestLogSeverity.Warning;

        return RequestLogSeverity.Information;
    }

    public static bool IsStaticAsset(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        foreach (var prefix in StaticPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var extension = Path.GetExtension(path);
        return !string.IsNullOrEmpty(extension)
               && StaticExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
