using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed record FiscalYearBackfillResult(int YearsCreated, int TransactionsAssigned, int TransactionsUnassigned);

/// <summary>
/// تکمیل داده‌ی قدیمی هنگام راه‌اندازی (idempotent؛ هر بار اجرا امن است):
///  ۱. اسنادی که FiscalYearId ندارند، به سالِ مالیِ شامل تاریخ‌شان وصل می‌شوند.
///  ۲. اگر برای بازه‌ی تاریخ این اسناد سالی تعریف نشده، سال‌های شمسیِ لازم (فروردین تا اسفند) ساخته می‌شود.
///  ۳. شرکتی که نه سال دارد و نه سند، سال شمسیِ جاری را می‌گیرد.
///
/// در زمان راه‌اندازی شرکت فعالی وجود ندارد؛ پس همه‌ی کوئری‌ها صراحتاً با IgnoreQueryFilters و
/// شناسه‌ی شرکت نوشته شده‌اند.
/// </summary>
public static class FiscalYearBackfill
{
    public static async Task<FiscalYearBackfillResult> RunAsync(ApplicationDbContext context, TimeProvider timeProvider)
    {
        var tenantIds = await context.Tenants
            .Where(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync();

        var created = 0;
        var assigned = 0;
        var unassigned = 0;

        foreach (var tenantId in tenantIds)
        {
            var result = await RunForTenantAsync(context, timeProvider, tenantId);

            created += result.YearsCreated;
            assigned += result.TransactionsAssigned;
            unassigned += result.TransactionsUnassigned;
        }

        return new FiscalYearBackfillResult(created, assigned, unassigned);
    }

    private static async Task<FiscalYearBackfillResult> RunForTenantAsync(
        ApplicationDbContext context, TimeProvider timeProvider, int tenantId)
    {
        var years = await context.FiscalYears
            .IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId && !f.IsDeleted)
            .ToListAsync();

        var legacy = context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.FiscalYearId == null);

        var createdCount = 0;

        if (!await legacy.AnyAsync())
        {
            // شرکت تازه: هنوز سالی ندارد
            if (years.Count == 0)
            {
                context.FiscalYears.Add(FiscalYearRules.NewPersianYear(tenantId, timeProvider.Today()));
                await context.SaveChangesAsync();
                createdCount = 1;
            }

            return new FiscalYearBackfillResult(createdCount, 0, 0);
        }

        // سال‌های شمسیِ لازم برای پوشش بازه‌ی تاریخ اسناد بدون سال
        var min = await legacy.MinAsync(t => t.Date);
        var max = await legacy.MaxAsync(t => t.Date);

        var cursor = PersianDate.StartOfYear(min);
        while (cursor <= max)
        {
            var start = cursor;
            var end = PersianDate.EndOfYear(cursor);

            var overlaps = years.Any(y => y.StartDate <= end && y.EndDate >= start);
            if (!overlaps)
            {
                var year = FiscalYearRules.NewPersianYear(tenantId, start);
                context.FiscalYears.Add(year);
                years.Add(year);
                createdCount++;
            }

            cursor = end.AddDays(1);
        }

        if (createdCount > 0)
            await context.SaveChangesAsync();

        // وصل کردن اسناد به سال (یک UPDATE به‌ازای هر سال؛ از Interceptor رد نمی‌شود و لازم هم نیست)
        var assignedCount = 0;
        foreach (var year in years)
        {
            var yearId = year.Id;
            var start = year.StartDate;
            var end = year.EndDate;

            assignedCount += await context.Transactions
                .IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId && t.FiscalYearId == null && t.Date >= start && t.Date <= end)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.FiscalYearId, yearId));
        }

        // اسنادی که در هیچ سالی نیفتادند (مثلاً بازه‌ی سفارشیِ نیمه‌همپوشان)
        var remaining = await legacy.CountAsync();

        return new FiscalYearBackfillResult(createdCount, assignedCount, remaining);
    }
}
