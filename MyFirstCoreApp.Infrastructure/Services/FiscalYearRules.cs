using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// قواعد مشترک سال مالی؛ تنها جایی که «سالِ باز برای یک تاریخ» پیدا می‌شود.
/// </summary>
internal static class FiscalYearRules
{
    /// <summary>
    /// سال مالیِ باز برای تاریخ را برمی‌گرداند و در غیر این صورت InvalidOperationException می‌دهد.
    /// اگر شرکت هنوز هیچ سال مالی ندارد، سال شمسیِ همان تاریخ به‌طور خودکار ساخته می‌شود
    /// (اولین سند شرکت نباید به‌خاطر تعریف‌نشدن سال متوقف شود).
    /// </summary>
    public static async Task<FiscalYear> RequireOpenAsync(
        ApplicationDbContext context, int tenantId, DateOnly date)
    {
        var year = await context.FiscalYears
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.StartDate <= date && f.EndDate >= date);

        if (year == null)
        {
            var hasAnyYear = await context.FiscalYears.AnyAsync(f => f.TenantId == tenantId);

            if (hasAnyYear)
            {
                throw new InvalidOperationException(
                    $"برای تاریخ {PersianDate.Format(date)} سال مالی تعریف نشده است. " +
                    "ابتدا آن را در بخش «سال‌های مالی» تعریف کنید.");
            }

            year = NewPersianYear(tenantId, date);
            context.FiscalYears.Add(year);
            await context.SaveChangesAsync();
        }

        if (year.Status == FiscalYearStatus.Closed)
        {
            throw new InvalidOperationException(
                $"{year.Name} بسته شده است؛ ثبت یا تغییر سند در آن ممکن نیست. " +
                "برای تغییر، ابتدا سال مالی را باز کنید.");
        }

        return year;
    }

    /// <summary>سال مالی ۱۲ ماهه‌ی شمسی که تاریخ داده‌شده در آن است (فروردین تا اسفند).</summary>
    public static FiscalYear NewPersianYear(int tenantId, DateOnly dateInYear)
    {
        var start = PersianDate.StartOfYear(dateInYear);
        var end = PersianDate.EndOfYear(dateInYear);

        return new FiscalYear
        {
            TenantId = tenantId,
            Name = DefaultName(start),
            StartDate = start,
            EndDate = end,
            Status = FiscalYearStatus.Open
        };
    }

    public static string DefaultName(DateOnly startDate)
        => $"سال مالی {PersianDate.ToParts(startDate).Year}";
}
