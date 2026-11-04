using System.Diagnostics;
using MyFirstCoreApp.Application.Common.Interfaces;
using Serilog;
using Serilog.Context;

namespace MyFirstCoreApp.Logging;

/// <summary>
/// شرکت فعال، کاربر و شناسه‌ی ردیابی را به «همه‌ی لاگ‌های همین درخواست» اضافه می‌کند
/// و به خلاصه‌ی درخواست (Serilog Request Logging) هم می‌دهد.
///
///  - باید بعد از UseSession و UseAuthentication بیاید (شرکت از Session/Claim خوانده می‌شود).
///  - TraceId همان مقداری است که صفحه‌ی خطا (/Home/Error) به کاربر نشان می‌دهد؛
///    کاربر «کد خطا» را بگوید، با همان در فایل لاگ جستجو می‌کنی.
///  - فقط شناسه‌ها لاگ می‌شوند، نه نام یا اطلاعات شخصی.
/// </summary>
public class LogContextMiddleware
{
    private readonly RequestDelegate _next;

    public LogContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IDiagnosticContext diagnosticContext,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        var tenantId = currentTenant.TenantId?.ToString() ?? "-";
        var userId = currentUser.UserId ?? "-";
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // برای خلاصه‌ی درخواست (که بعد از پایان درخواست و بیرون از این Scope نوشته می‌شود)
        diagnosticContext.Set("TenantId", tenantId);
        diagnosticContext.Set("UserId", userId);
        diagnosticContext.Set("TraceId", traceId);

        // برای همه‌ی لاگ‌هایی که حین اجرای درخواست نوشته می‌شوند (سرویس‌ها، EF، ...)
        using (LogContext.PushProperty("TenantId", tenantId))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            await _next(context);
        }
    }
}
