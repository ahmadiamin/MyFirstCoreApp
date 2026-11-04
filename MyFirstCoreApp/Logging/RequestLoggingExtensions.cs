using MyFirstCoreApp.Application.Common.Logging;
using Serilog;
using Serilog.Events;

namespace MyFirstCoreApp.Logging;

public static class RequestLoggingExtensions
{
    /// <summary>
    /// خلاصه‌ی هر درخواست (روش، مسیر، وضعیت، زمان، شرکت و کاربر). سطح لاگ با RequestLogPolicy تعیین می‌شود:
    /// خطا ← Error، دسترسی ردشده و درخواست کند ← Warning، فایل ثابت ← بی‌صدا.
    /// فقط «مسیر» لاگ می‌شود (QueryString و بدنه‌ی درخواست هرگز؛ ممکن است اطلاعات حساس داشته باشند).
    /// </summary>
    public static IApplicationBuilder UseAppRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} → {StatusCode} در {Elapsed:0} ms";

            options.GetLevel = (httpContext, elapsedMs, exception) =>
                RequestLogPolicy.Classify(
                    httpContext.Response.StatusCode,
                    exception != null,
                    httpContext.Request.Path.Value,
                    elapsedMs) switch
                {
                    RequestLogSeverity.Error => LogEventLevel.Error,
                    RequestLogSeverity.Warning => LogEventLevel.Warning,
                    RequestLogSeverity.Verbose => LogEventLevel.Verbose,
                    _ => LogEventLevel.Information
                };
        });
    }
}
