using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// تکمیل داده‌ی قدیمی هنگام راه‌اندازی. در آن لحظه شرکت فعالی وجود ندارد (Tenant = null)،
/// پس همه‌ی تست‌ها هم در همین حالت اجرا می‌شوند.
/// </summary>
public class FiscalYearBackfillTests : IDisposable
{
    private readonly TestDatabase _db = new();

    // امروز (ایران) = 2026-03-21 = 1405/01/01
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

    public FiscalYearBackfillTests()
    {
        _db.Tenant.TenantId = null;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreatesMissingPersianYears_AndAssignsLegacyTransactions()
    {
        var tenantId = await AddTenantAsync(7);
        await AddLegacyTransactionAsync(tenantId, 1, new DateOnly(2025, 6, 1));   // 1404
        await AddLegacyTransactionAsync(tenantId, 2, new DateOnly(2026, 7, 1));   // 1405
        await AddLegacyTransactionAsync(tenantId, 3, new DateOnly(2026, 8, 1));   // 1405

        var result = await RunAsync();

        Assert.Equal(new FiscalYearBackfillResult(YearsCreated: 2, TransactionsAssigned: 3, TransactionsUnassigned: 0), result);

        await using var verify = _db.NewContext();
        var years = await verify.FiscalYears.IgnoreQueryFilters()
            .Where(f => f.TenantId == tenantId)
            .OrderBy(f => f.StartDate)
            .ToListAsync();

        Assert.Equal(2, years.Count);
        Assert.Equal("سال مالی 1404", years[0].Name);
        Assert.Equal(new DateOnly(2025, 3, 21), years[0].StartDate);
        Assert.Equal(new DateOnly(2026, 3, 20), years[0].EndDate);
        Assert.Equal("سال مالی 1405", years[1].Name);
        Assert.Equal(new DateOnly(2027, 3, 20), years[1].EndDate);
        Assert.All(years, y => Assert.Equal(FiscalYearStatus.Open, y.Status));

        var transactions = await verify.Transactions.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Number)
            .ToListAsync();

        Assert.Equal(years[0].Id, transactions[0].FiscalYearId);
        Assert.Equal(years[1].Id, transactions[1].FiscalYearId);
        Assert.Equal(years[1].Id, transactions[2].FiscalYearId);
    }

    [Fact]
    public async Task IsIdempotent_SecondRunChangesNothing()
    {
        var tenantId = await AddTenantAsync(7);
        await AddLegacyTransactionAsync(tenantId, 1, new DateOnly(2026, 7, 1));

        await RunAsync();
        var second = await RunAsync();

        Assert.Equal(new FiscalYearBackfillResult(0, 0, 0), second);

        await using var verify = _db.NewContext();
        Assert.Equal(1, await verify.FiscalYears.IgnoreQueryFilters().CountAsync(f => f.TenantId == tenantId));
    }

    [Fact]
    public async Task NewTenantWithoutDocuments_GetsTheCurrentPersianYear()
    {
        var tenantId = await AddTenantAsync(8);

        var result = await RunAsync();

        Assert.Equal(1, result.YearsCreated);

        await using var verify = _db.NewContext();
        var year = await verify.FiscalYears.IgnoreQueryFilters().SingleAsync(f => f.TenantId == tenantId);

        Assert.Equal("سال مالی 1405", year.Name);
        Assert.Equal(new DateOnly(2026, 3, 21), year.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 20), year.EndDate);
    }

    [Fact]
    public async Task ExistingCustomYear_IsRespected_AndUncoveredTransactionsAreReported()
    {
        var tenantId = await AddTenantAsync(9);

        // سال سفارشیِ غیرشمسی که با سال‌های شمسی همپوشانی دارد
        await using (var context = _db.NewContext())
        {
            context.FiscalYears.Add(new FiscalYear
            {
                TenantId = tenantId,
                Name = "سال سفارشی",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                Status = FiscalYearStatus.Open
            });
            await context.SaveChangesAsync();
        }

        await AddLegacyTransactionAsync(tenantId, 1, new DateOnly(2026, 2, 1));    // داخل سال سفارشی
        await AddLegacyTransactionAsync(tenantId, 2, new DateOnly(2027, 1, 15));   // بیرون از آن، ولی همپوشان با سال شمسی 1405

        var result = await RunAsync();

        // سال تازه نساخته می‌شود (همپوشانی)؛ سند بیرون از سال‌ها «وصل‌نشده» گزارش می‌شود
        Assert.Equal(new FiscalYearBackfillResult(YearsCreated: 0, TransactionsAssigned: 1, TransactionsUnassigned: 1), result);
    }

    [Fact]
    public async Task OnlyTouchesTransactionsOfTheirOwnTenant()
    {
        var tenantA = await AddTenantAsync(7);
        var tenantB = await AddTenantAsync(8);
        await AddLegacyTransactionAsync(tenantA, 1, new DateOnly(2026, 7, 1));
        await AddLegacyTransactionAsync(tenantB, 1, new DateOnly(2026, 7, 1));

        await RunAsync();

        await using var verify = _db.NewContext();
        var yearOfA = await verify.FiscalYears.IgnoreQueryFilters().SingleAsync(f => f.TenantId == tenantA);
        var yearOfB = await verify.FiscalYears.IgnoreQueryFilters().SingleAsync(f => f.TenantId == tenantB);

        Assert.NotEqual(yearOfA.Id, yearOfB.Id);

        var transactionOfA = await verify.Transactions.IgnoreQueryFilters().SingleAsync(t => t.TenantId == tenantA);
        var transactionOfB = await verify.Transactions.IgnoreQueryFilters().SingleAsync(t => t.TenantId == tenantB);

        Assert.Equal(yearOfA.Id, transactionOfA.FiscalYearId);
        Assert.Equal(yearOfB.Id, transactionOfB.FiscalYearId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<FiscalYearBackfillResult> RunAsync()
    {
        await using var context = _db.NewContext();
        return await FiscalYearBackfill.RunAsync(context, _time);
    }

    private async Task<int> AddTenantAsync(int id)
    {
        await using var context = _db.NewContext();
        context.Tenants.Add(new Tenant { Id = id, Name = $"شرکت {id}" });
        await context.SaveChangesAsync();
        return id;
    }

    /// <summary>سند «قدیمی»: مستقیم و بدون سرویس، بدون FiscalYearId.</summary>
    private async Task AddLegacyTransactionAsync(int tenantId, int number, DateOnly date)
    {
        var debit = await _db.AddAccountAsync(tenantId, $"بدهکار {number}");
        var credit = await _db.AddAccountAsync(tenantId, $"بستانکار {number}");

        await using var context = _db.NewContext();
        context.Transactions.Add(new Transaction
        {
            TenantId = tenantId,
            Number = number,
            Date = date,
            IsPosted = true,
            TotalAmount = 100,
            Lines =
            {
                new TransactionLine { TenantId = tenantId, LineNumber = 1, AccountId = debit, Debit = 100 },
                new TransactionLine { TenantId = tenantId, LineNumber = 2, AccountId = credit, Credit = 100 }
            }
        });

        await context.SaveChangesAsync();
    }
}
