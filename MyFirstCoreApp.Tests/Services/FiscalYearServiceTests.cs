using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class FiscalYearServiceTests : IDisposable
{
    // شرکت 4: بدون سال مالی در TestDatabase
    private const int Tenant = 4;

    private readonly TestDatabase _db = new();

    // 2026-03-20 21:00 UTC = 2026-03-21 00:30 ایران ← امروز = 1405/01/01
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly Y1405Start = new(2026, 3, 21);
    private static readonly DateOnly Y1405End = new(2027, 3, 20);
    private static readonly DateOnly Y1406Start = new(2027, 3, 21);
    private static readonly DateOnly Y1406End = new(2028, 3, 19);

    public FiscalYearServiceTests()
    {
        _db.Tenant.TenantId = Tenant;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // ثبت
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_WithoutName_UsesPersianStartYear_AndIsOpen()
    {
        var year = await CreateAsync(Y1405Start, Y1405End);

        Assert.Equal("سال مالی 1405", year.Name);
        Assert.Equal(FiscalYearStatus.Open, year.Status);
        Assert.Equal(Tenant, year.TenantId);
    }

    [Fact]
    public async Task Create_RejectsInvalidRanges()
    {
        await CreateAsync(Y1405Start, Y1405End);

        var cases = new (DateOnly Start, DateOnly End, string Expected)[]
        {
            (default, Y1406End, "تاریخ شروع"),
            (Y1406Start, default, "تاریخ پایان"),
            (Y1406End, Y1406Start, "قبل از تاریخ شروع"),
            (Y1406Start, Y1406Start.AddDays(500), "400"),
            (new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31), "همپوشانی"),     // وسط سال 1405
            (new DateOnly(2025, 6, 1), Y1405Start, "همپوشانی"),                    // روز اول 1405 مشترک
        };

        foreach (var (start, end, expected) in cases)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(start, end));
            Assert.Contains(expected, ex.Message);
        }
    }

    [Fact]
    public async Task Create_AdjacentYears_AreAllowed()
    {
        await CreateAsync(Y1405Start, Y1405End);
        var next = await CreateAsync(Y1406Start, Y1406End);

        Assert.Equal("سال مالی 1406", next.Name);
    }

    [Fact]
    public async Task Create_DoesNotConflictWithAnotherTenantsYears()
    {
        await CreateAsync(Y1405Start, Y1405End);

        _db.Tenant.TenantId = 5;
        await using var context = _db.NewContext();
        var year = await NewService(context).CreateAsync(
            new FiscalYearRequest { StartDate = Y1405Start, EndDate = Y1405End }, 5);

        Assert.Equal(5, year.TenantId);
    }

    // ---------------------------------------------------------------
    // ویرایش
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_OpenYear_ChangesNameAndRange()
    {
        var year = await CreateAsync(Y1405Start, Y1405End);

        await using (var context = _db.NewContext())
            await NewService(context).UpdateAsync(year.Id,
                new FiscalYearRequest { Name = "سال اول", StartDate = Y1405Start, EndDate = new DateOnly(2027, 3, 31) }, Tenant);

        var loaded = await LoadAsync(year.Id);
        Assert.Equal("سال اول", loaded.Name);
        Assert.Equal(new DateOnly(2027, 3, 31), loaded.EndDate);
    }

    [Fact]
    public async Task Update_ThatWouldExcludeExistingTransactions_IsRejected()
    {
        var year = await CreateAsync(Y1405Start, Y1405End);
        await AddPostedTransactionAsync(new DateOnly(2026, 11, 1));

        // کوتاه‌کردن سال به قبل از تاریخ سند
        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(context).UpdateAsync(year.Id,
                new FiscalYearRequest { StartDate = Y1405Start, EndDate = new DateOnly(2026, 9, 22) }, Tenant));

        Assert.Contains("خارج از بازه", ex.Message);
    }

    [Fact]
    public async Task Update_ClosedYear_IsRejected()
    {
        var year = await CreateAsync(Y1405Start, Y1405End);

        await using (var context = _db.NewContext())
            await NewService(context).CloseAsync(year.Id, Tenant);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NewService(context).UpdateAsync(year.Id,
                    new FiscalYearRequest { StartDate = Y1405Start, EndDate = Y1405End }, Tenant));
    }

    // ---------------------------------------------------------------
    // بستن / باز کردن
    // ---------------------------------------------------------------

    [Fact]
    public async Task Close_RecordsWhoAndWhen_AndRejectsRepeat()
    {
        var year = await CreateAsync(Y1405Start, Y1405End);

        await using (var context = _db.NewContext())
            await NewService(context).CloseAsync(year.Id, Tenant);

        var loaded = await LoadAsync(year.Id);
        Assert.Equal(FiscalYearStatus.Closed, loaded.Status);
        Assert.Equal("user-1", loaded.ClosedBy);
        Assert.Equal(new DateTime(2026, 3, 20, 21, 0, 0, DateTimeKind.Utc), loaded.ClosedAt);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).CloseAsync(year.Id, Tenant));
    }

    [Fact]
    public async Task Close_WithDraftTransactions_IsRejected_UntilTheyArePosted()
    {
        var year = await CreateAsync(Y1405Start, Y1405End);
        var draftId = await AddTransactionAsync(new DateOnly(2026, 5, 1), post: false);

        await using (var context = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).CloseAsync(year.Id, Tenant));
            Assert.Contains("پیش‌نویس", ex.Message);
        }

        await using (var context = _db.NewContext())
            await new TransactionService(context).PostAsync(draftId, Tenant);

        await using (var context = _db.NewContext())
            await NewService(context).CloseAsync(year.Id, Tenant);

        Assert.Equal(FiscalYearStatus.Closed, (await LoadAsync(year.Id)).Status);
    }

    [Fact]
    public async Task Reopen_ClearsClosedInfo_ButNotWhileALaterYearIsClosed()
    {
        var y1405 = await CreateAsync(Y1405Start, Y1405End);
        var y1406 = await CreateAsync(Y1406Start, Y1406End);

        await using (var context = _db.NewContext())
            await NewService(context).CloseAsync(y1405.Id, Tenant);
        await using (var context = _db.NewContext())
            await NewService(context).CloseAsync(y1406.Id, Tenant);

        // 1405 را نمی‌شود باز کرد چون 1406 بسته است
        await using (var context = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).ReopenAsync(y1405.Id, Tenant));
            Assert.Contains("بعدی", ex.Message);
        }

        await using (var context = _db.NewContext())
            await NewService(context).ReopenAsync(y1406.Id, Tenant);
        await using (var context = _db.NewContext())
            await NewService(context).ReopenAsync(y1405.Id, Tenant);

        var loaded = await LoadAsync(y1405.Id);
        Assert.Equal(FiscalYearStatus.Open, loaded.Status);
        Assert.Null(loaded.ClosedAt);
        Assert.Null(loaded.ClosedBy);
    }

    // ---------------------------------------------------------------
    // حذف
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_EmptyOpenYear_IsSoftDeleted()
    {
        var year = await CreateAsync(Y1405Start, Y1405End);

        _db.User.UserId = "deleter";
        await using (var context = _db.NewContext())
            await NewService(context).DeleteAsync(year.Id, Tenant);

        await using var verify = _db.NewContext();
        Assert.Empty(await verify.FiscalYears.ToListAsync());

        var deleted = await verify.SetIncludingDeleted<FiscalYear>().SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.Equal("deleter", deleted.DeletedBy);
    }

    [Fact]
    public async Task Delete_YearWithTransactions_OrClosedYear_IsRejected()
    {
        var withDocs = await CreateAsync(Y1405Start, Y1405End);
        await AddPostedTransactionAsync(new DateOnly(2026, 5, 1));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).DeleteAsync(withDocs.Id, Tenant));

        var closed = await CreateAsync(Y1406Start, Y1406End);
        await using (var context = _db.NewContext())
            await NewService(context).CloseAsync(closed.Id, Tenant);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => NewService(context).DeleteAsync(closed.Id, Tenant));
    }

    // ---------------------------------------------------------------
    // سال جاری
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetCurrent_ReturnsYearContainingTodayInIran_ElseLatest_ElseNull()
    {
        await using (var context = _db.NewContext())
            Assert.Null(await NewService(context).GetCurrentAsync(Tenant));

        var y1404 = await CreateAsync(new DateOnly(2025, 3, 21), new DateOnly(2026, 3, 20));

        // امروز (به وقت ایران) 2026-03-21 است؛ در هیچ سالی نیست ← آخرین سال
        await using (var context = _db.NewContext())
            Assert.Equal(y1404.Id, (await NewService(context).GetCurrentAsync(Tenant))!.Id);

        var y1405 = await CreateAsync(Y1405Start, Y1405End);

        // حالا امروز در 1405 است (نه در 1404؛ ساعت 00:30 ایران، هنوز روز قبل در UTC)
        await using (var context = _db.NewContext())
            Assert.Equal(y1405.Id, (await NewService(context).GetCurrentAsync(Tenant))!.Id);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private FiscalYearService NewService(ApplicationDbContext context)
        => new(context, _db.User, _time);

    private async Task<FiscalYear> CreateAsync(DateOnly start, DateOnly end, string? name = null)
    {
        await using var context = _db.NewContext();
        return await NewService(context).CreateAsync(
            new FiscalYearRequest { Name = name, StartDate = start, EndDate = end }, Tenant);
    }

    private async Task<FiscalYear> LoadAsync(int id)
    {
        await using var context = _db.NewContext();
        return await context.FiscalYears.SingleAsync(f => f.Id == id);
    }

    private Task AddPostedTransactionAsync(DateOnly date) => AddTransactionAsync(date, post: true);

    private async Task<int> AddTransactionAsync(DateOnly date, bool post)
    {
        var debit = await _db.AddAccountAsync(Tenant, "حساب بدهکار " + date);
        var credit = await _db.AddAccountAsync(Tenant, "حساب بستانکار " + date);

        await using var context = _db.NewContext();
        var service = new TransactionService(context);

        var created = await service.CreateAsync(new Transaction
        {
            TenantId = Tenant,
            Date = date,
            Lines =
            {
                new TransactionLine { AccountId = debit, Debit = 100 },
                new TransactionLine { AccountId = credit, Credit = 100 }
            }
        });

        if (post)
            await service.PostAsync(created.Id, Tenant);

        return created.Id;
    }
}
