using MyFirstCoreApp.Application.Dashboard;

namespace MyFirstCoreApp.Application.Interfaces;

/// <summary>
/// خلاصه‌ی مالی داشبورد برای «شرکت فعال» (با یک Stored Procedure).
/// بازه‌ی فروش/خرید و نمودار = سال مالی جاری (یا سال شمسی جاری اگر سالی تعریف نشده)؛
/// چک‌های «سررسیدشده یا نزدیک» = تا dueWindowDays روز بعد از امروز.
/// </summary>
public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(int dueWindowDays = 7, int topN = 5, CancellationToken cancellationToken = default);
}
