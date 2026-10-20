using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// قفل سال مالی در TransactionService؛ چون فاکتور، پرداخت و چک همگی سند را از همین سرویس می‌سازند،
/// این تست‌ها قفل را برای همه‌ی ماژول‌ها تضمین می‌کنند.
/// </summary>
public class TransactionFiscalYearTests : IDisposable
{
    // شرکت 6: بدون سال مالی در TestDatabase
    private const int Tenant = 6;

    private readonly TestDatabase _db = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 3, 20, 21, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly In1405 = new(2026, 7, 1);
    private static readonly DateOnly In1406 = new(2027, 7, 1);

    private int _debitAccount;
    private int _creditAccount;

    public TransactionFiscalYearTests()
    {
        _db.Tenant.TenantId = Tenant;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // انتساب سال
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_AssignsTheYearContainingTheDate()
    {
        var y1405 = await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));
        var y1406 = await CreateYearAsync(new DateOnly(2027, 3, 21), new DateOnly(2028, 3, 19));

        var first = await CreateTransactionAsync(In1405);
        var second = await CreateTransactionAsync(In1406);

        Assert.Equal(y1405.Id, first.FiscalYearId);
        Assert.Equal(y1406.Id, second.FiscalYearId);
    }

    [Fact]
    public async Task Create_OnBoundaryDays_BelongsToTheirOwnYear()
    {
        var y1405 = await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));
        var y1406 = await CreateYearAsync(new DateOnly(2027, 3, 21), new DateOnly(2028, 3, 19));

        Assert.Equal(y1405.Id, (await CreateTransactionAsync(new DateOnly(2027, 3, 20))).FiscalYearId); // آخرین روز 1405
        Assert.Equal(y1406.Id, (await CreateTransactionAsync(new DateOnly(2027, 3, 21))).FiscalYearId); // اولین روز 1406
        Assert.Equal(y1405.Id, (await CreateTransactionAsync(new DateOnly(2026, 3, 21))).FiscalYearId); // اولین روز 1405
    }

    [Fact]
    public async Task Create_ForTenantWithoutAnyYear_CreatesThePersianYearOfTheDocumentDate()
    {
        var created = await CreateTransactionAsync(new DateOnly(2026, 5, 1));

        await using var verify = _db.NewContext();
        var year = await verify.FiscalYears.SingleAsync();

        Assert.Equal(year.Id, created.FiscalYearId);
        Assert.Equal("سال مالی 1405", year.Name);
        Assert.Equal(new DateOnly(2026, 3, 21), year.StartDate);
        Assert.Equal(new DateOnly(2027, 3, 20), year.EndDate);
        Assert.Equal(FiscalYearStatus.Open, year.Status);
    }

    [Fact]
    public async Task Create_OutsideEveryDefinedYear_IsRejected_WithoutCreatingOne()
    {
        await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTransactionAsync(In1406));
        Assert.Contains("سال مالی تعریف نشده", ex.Message);
        Assert.Contains("1406", ex.Message);   // تاریخ شمسی در پیام

        await using var verify = _db.NewContext();
        Assert.Equal(1, await verify.FiscalYears.CountAsync());
        Assert.Empty(await verify.Transactions.ToListAsync());
    }

    // ---------------------------------------------------------------
    // قفل سال بسته
    // ---------------------------------------------------------------

    [Fact]
    public async Task ClosedYear_BlocksCreate_Unpost_AndModuleDelete_UntilReopened()
    {
        var year = await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));

        var posted = await CreateTransactionAsync(In1405);
        await PostAsync(posted.Id);

        await using (var context = _db.NewContext())
            await new FiscalYearService(context, _db.User, _time).CloseAsync(year.Id, Tenant);

        // ثبت سند جدید در سال بسته
        await AssertClosedAsync(() => CreateTransactionAsync(new DateOnly(2026, 9, 1)));

        // برگشت به پیش‌نویس
        await AssertClosedAsync(() => UnpostAsync(posted.Id));

        // حذف از مسیر ماژول‌ها (مثلاً Unpost فاکتور/پرداخت/چک)
        await using (var context = _db.NewContext())
            await AssertClosedAsync(() => new TransactionService(context).DeleteAsync(posted.Id, Tenant));

        // بعد از باز کردن سال، همه‌چیز دوباره کار می‌کند
        await using (var context = _db.NewContext())
            await new FiscalYearService(context, _db.User, _time).ReopenAsync(year.Id, Tenant);

        await UnpostAsync(posted.Id);
        await DeleteManualAsync(posted.Id);
        await CreateTransactionAsync(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public async Task ClosedYear_BlocksPost_Update_AndDelete_OfADraftWhoseYearClosedMeanwhile()
    {
        // سناریوی «بسته شدن سال بین دو عملیات»: سند در سالِ باز ساخته می‌شود، سپس سال بسته می‌شود.
        // (CloseAsync با وجود پیش‌نویس رد می‌شود؛ پس بسته‌شدن را مستقیم در دیتابیس شبیه‌سازی می‌کنیم.)
        var year = await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));
        var draft = await CreateTransactionAsync(In1405);

        await using (var context = _db.NewContext())
        {
            var tracked = await context.FiscalYears.SingleAsync(f => f.Id == year.Id);
            tracked.Status = FiscalYearStatus.Closed;
            await context.SaveChangesAsync();
        }

        await AssertClosedAsync(() => PostAsync(draft.Id));
        await AssertClosedAsync(() => DeleteManualAsync(draft.Id));

        var edit = NewTransaction(In1405);
        edit.Id = draft.Id;

        await using var context2 = _db.NewContext();
        await AssertClosedAsync(() => new TransactionService(context2).UpdateAsync(edit));
    }

    // ---------------------------------------------------------------
    // انتقال بین سال‌ها در ویرایش
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_MovingTheDateToAnotherYear_ReassignsTheFiscalYear()
    {
        await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));
        var y1406 = await CreateYearAsync(new DateOnly(2027, 3, 21), new DateOnly(2028, 3, 19));
        var created = await CreateTransactionAsync(In1405);

        var edit = NewTransaction(In1406);
        edit.Id = created.Id;

        await using (var context = _db.NewContext())
            await new TransactionService(context).UpdateAsync(edit);

        await using var verify = _db.NewContext();
        var loaded = await verify.Transactions.SingleAsync(t => t.Id == created.Id);

        Assert.Equal(In1406, loaded.Date);
        Assert.Equal(y1406.Id, loaded.FiscalYearId);
    }

    [Fact]
    public async Task Update_MovingTheDateIntoAClosedYear_IsRejected()
    {
        await CreateYearAsync(new DateOnly(2026, 3, 21), new DateOnly(2027, 3, 20));
        var y1406 = await CreateYearAsync(new DateOnly(2027, 3, 21), new DateOnly(2028, 3, 19));
        var created = await CreateTransactionAsync(In1405);

        await using (var context = _db.NewContext())
            await new FiscalYearService(context, _db.User, _time).CloseAsync(y1406.Id, Tenant);

        var edit = NewTransaction(In1406);
        edit.Id = created.Id;

        await using var service = _db.NewContext();
        await AssertClosedAsync(() => new TransactionService(service).UpdateAsync(edit));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task EnsureAccountsAsync()
    {
        if (_debitAccount != 0) return;

        _debitAccount = await _db.AddAccountAsync(Tenant, "بدهکار");
        _creditAccount = await _db.AddAccountAsync(Tenant, "بستانکار");
    }

    private Transaction NewTransaction(DateOnly date) => new()
    {
        TenantId = Tenant,
        Date = date,
        Description = "تست سال مالی",
        Lines =
        {
            new TransactionLine { AccountId = _debitAccount, Debit = 500 },
            new TransactionLine { AccountId = _creditAccount, Credit = 500 }
        }
    };

    private async Task<Transaction> CreateTransactionAsync(DateOnly date)
    {
        await EnsureAccountsAsync();

        await using var context = _db.NewContext();
        return await new TransactionService(context).CreateAsync(NewTransaction(date));
    }

    private async Task<FiscalYear> CreateYearAsync(DateOnly start, DateOnly end)
    {
        await using var context = _db.NewContext();
        return await new FiscalYearService(context, _db.User, _time)
            .CreateAsync(new FiscalYearRequest { StartDate = start, EndDate = end }, Tenant);
    }

    private async Task PostAsync(int id)
    {
        await using var context = _db.NewContext();
        await new TransactionService(context).PostAsync(id, Tenant);
    }

    private async Task UnpostAsync(int id)
    {
        await using var context = _db.NewContext();
        await new TransactionService(context).UnpostAsync(id, Tenant);
    }

    private async Task DeleteManualAsync(int id)
    {
        await using var context = _db.NewContext();
        await new TransactionService(context).DeleteManualAsync(id, Tenant);
    }

    private static async Task AssertClosedAsync(Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Contains("بسته", ex.Message);
    }
}
