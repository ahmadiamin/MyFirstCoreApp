using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

/// <summary>
/// تصمیم می‌گیرد «مانده‌ی اول دوره» از کدام تاریخ شروع به جمع شدن کند.
///
///  - حساب‌های دائمی (دارایی، بدهی، سرمایه): null ← از ابتدای تاریخ‌ها (تجمعی).
///  - حساب‌های موقت (درآمد و هزینه): ابتدای سال مالیِ شامل تاریخ شروع گزارش؛
///    چون این حساب‌ها در شروع هر سال مالی صفر می‌شوند (معادل سند اختتامیه، بدون ساختن سند).
///    اگر برای آن تاریخ سال مالی تعریف نشده باشد، ابتدای سال شمسی همان تاریخ.
/// </summary>
public static class ReportOpeningPolicy
{
    public static bool IsTemporaryAccount(AccountType accountType)
        => accountType is AccountType.Income or AccountType.Expense;

    public static async Task<DateOnly?> ResolveAsync(
        ApplicationDbContext context,
        int tenantId,
        AccountType accountType,
        DateOnly reportFrom,
        CancellationToken cancellationToken = default)
    {
        if (!IsTemporaryAccount(accountType))
            return null;

        var fiscalYearStart = await context.FiscalYears
            .Where(f => f.TenantId == tenantId && f.StartDate <= reportFrom && f.EndDate >= reportFrom)
            .Select(f => (DateOnly?)f.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        return fiscalYearStart ?? PersianDate.StartOfYear(reportFrom);
    }
}
