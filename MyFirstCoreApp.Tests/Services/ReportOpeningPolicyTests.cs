using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class ReportOpeningPolicyTests : IDisposable
{
    // شرکت 4 و 5: در TestDatabase سال مالی ندارند
    private const int Tenant = 4;

    private readonly TestDatabase _db = new();

    public ReportOpeningPolicyTests()
    {
        _db.Tenant.TenantId = Tenant;
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(AccountType.Bank)]
    [InlineData(AccountType.Cash)]
    [InlineData(AccountType.Receivable)]
    [InlineData(AccountType.Payable)]
    [InlineData(AccountType.Capital)]
    [InlineData(AccountType.Contra)]
    public async Task PermanentAccounts_AreCumulative(AccountType type)
    {
        await AddYearAsync(Tenant, new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));

        var result = await ResolveAsync(Tenant, type, new DateOnly(2026, 8, 1));

        Assert.Null(result);
    }

    [Theory]
    [InlineData(AccountType.Income)]
    [InlineData(AccountType.Expense)]
    public async Task TemporaryAccounts_StartAtTheBeginningOfTheFiscalYear(AccountType type)
    {
        await AddYearAsync(Tenant, new DateOnly(2025, 3, 21), new DateOnly(2026, 3, 20));
        await AddYearAsync(Tenant, new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));

        Assert.Equal(new DateOnly(2025, 3, 21), await ResolveAsync(Tenant, type, new DateOnly(2025, 12, 1)));
        Assert.Equal(new DateOnly(2026, 3, 21), await ResolveAsync(Tenant, type, new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public async Task TemporaryAccounts_OnTheBoundaryDays_BelongToTheirOwnYear()
    {
        await AddYearAsync(Tenant, new DateOnly(2025, 3, 21), new DateOnly(2026, 3, 20));
        await AddYearAsync(Tenant, new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));

        // اولین روز سال جدید: مانده‌ی اول دوره صفر است (از همان روز شروع می‌شود)
        Assert.Equal(new DateOnly(2026, 3, 21), await ResolveAsync(Tenant, AccountType.Income, new DateOnly(2026, 3, 21)));
        // آخرین روز سال قبل هنوز در سال قبل است
        Assert.Equal(new DateOnly(2025, 3, 21), await ResolveAsync(Tenant, AccountType.Income, new DateOnly(2026, 3, 20)));
    }

    [Fact]
    public async Task WithoutAnyFiscalYear_FallsBackToThePersianYearStart()
    {
        // 2026-08-01 = 1405/05/10 ← ابتدای سال شمسی 1405 = 2026-03-21
        var result = await ResolveAsync(5, AccountType.Expense, new DateOnly(2026, 8, 1));

        Assert.Equal(new DateOnly(2026, 3, 21), result);
    }

    [Fact]
    public async Task OtherTenantsFiscalYears_AreIgnored()
    {
        // سال سفارشیِ شرکت 5 نباید روی شرکت 4 اثر بگذارد
        await AddYearAsync(5, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var result = await ResolveAsync(Tenant, AccountType.Income, new DateOnly(2026, 8, 1));

        Assert.Equal(new DateOnly(2026, 3, 21), result);   // سال شمسی، نه سال سفارشی شرکت 5
    }

    private async Task<DateOnly?> ResolveAsync(int tenantId, AccountType type, DateOnly from)
    {
        await using var context = _db.NewContext();
        return await ReportOpeningPolicy.ResolveAsync(context, tenantId, type, from);
    }

    private async Task AddYearAsync(int tenantId, DateOnly start, DateOnly end)
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenantId;

        try
        {
            await using var context = _db.NewContext();
            context.FiscalYears.Add(new FiscalYear
            {
                TenantId = tenantId,
                Name = $"سال {start:yyyy}",
                StartDate = start,
                EndDate = end,
                Status = FiscalYearStatus.Open
            });
            await context.SaveChangesAsync();
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }
}
