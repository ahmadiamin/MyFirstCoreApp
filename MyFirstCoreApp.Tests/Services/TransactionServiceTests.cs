using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public class TransactionServiceTests : IDisposable
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    private readonly TestDatabase _db = new();

    public TransactionServiceTests()
    {
        _db.Tenant.TenantId = TenantA;
        _db.User.UserId = "user-1";
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // ثبت
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_ValidDocument_AssignsNumberLineNumbersAndTotal()
    {
        var (debit, credit) = await TwoAccountsAsync();

        var created = await CreateAsync(NewTransaction(debit, credit, 1_000));

        Assert.Equal(1, created.Number);
        Assert.Equal(1_000, created.TotalAmount);
        Assert.Equal(new[] { 1, 2 }, created.Lines.Select(l => l.LineNumber).OrderBy(n => n).ToArray());
        Assert.All(created.Lines, l => Assert.Equal(TenantA, l.TenantId));
    }

    [Fact]
    public async Task Create_SecondDocument_GetsNextNumber()
    {
        var (debit, credit) = await TwoAccountsAsync();

        await CreateAsync(NewTransaction(debit, credit));
        var second = await CreateAsync(NewTransaction(debit, credit));

        Assert.Equal(2, second.Number);
    }

    [Fact]
    public async Task Create_Unbalanced_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var transaction = NewTransaction(debit, credit, 1_000);
        transaction.Lines.Last().Credit = 900;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(transaction));
        Assert.Contains("برابر نیست", ex.Message);
    }

    [Fact]
    public async Task Create_LineWithBothDebitAndCredit_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var transaction = NewTransaction(debit, credit, 1_000);
        transaction.Lines.First().Credit = 500;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(transaction));
    }

    [Fact]
    public async Task Create_FractionalAmount_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAsync(NewTransaction(debit, credit, 1_000.5m)));

        Assert.Contains("عدد صحیح", ex.Message);
    }

    [Fact]
    public async Task Create_WithoutDate_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var transaction = NewTransaction(debit, credit);
        transaction.Date = default;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(transaction));
        Assert.Contains("تاریخ", ex.Message);
    }

    [Fact]
    public async Task Create_WithSingleLine_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var transaction = NewTransaction(debit, credit);
        transaction.Lines.Remove(transaction.Lines.Last());

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(transaction));
    }

    [Fact]
    public async Task Create_WithAccountOfAnotherTenant_Throws()
    {
        var (debit, _) = await TwoAccountsAsync();
        var foreignAccount = await _db.AddAccountAsync(TenantB, "حساب شرکت دیگر");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAsync(NewTransaction(debit, foreignAccount)));

        Assert.Contains("حساب", ex.Message);
    }

    [Fact]
    public async Task Create_AfterDeletingLastDocument_DoesNotReuseItsNumber()
    {
        var (debit, credit) = await TwoAccountsAsync();

        var first = await CreateAsync(NewTransaction(debit, credit));
        await using (var context = _db.NewContext())
            await new TransactionService(context).DeleteManualAsync(first.Id, TenantA);

        var next = await CreateAsync(NewTransaction(debit, credit));

        Assert.Equal(2, next.Number);
    }

    // ---------------------------------------------------------------
    // ویرایش
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_DraftDocument_ReplacesHeaderAndLines()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var created = await CreateAsync(NewTransaction(debit, credit, 1_000));

        var edited = NewTransaction(debit, credit, 2_500);
        edited.Id = created.Id;
        edited.Date = new DateOnly(2026, 4, 1);
        edited.Description = "ویرایش‌شده";

        await using (var context = _db.NewContext())
            await new TransactionService(context).UpdateAsync(edited);

        await using var verify = _db.NewContext();
        var loaded = await new TransactionService(verify).GetByIdAsync(created.Id, TenantA);

        Assert.NotNull(loaded);
        Assert.Equal(new DateOnly(2026, 4, 1), loaded!.Date);
        Assert.Equal("ویرایش‌شده", loaded.Description);
        Assert.Equal(2_500, loaded.TotalAmount);
        Assert.Equal(2, loaded.Lines.Count);

        // خطوط قدیمی حذف نرم شده‌اند، نه واقعی
        var allLines = await verify.TransactionLines.IgnoreQueryFilters().CountAsync();
        Assert.Equal(4, allLines);
    }

    [Fact]
    public async Task Update_PostedDocument_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var created = await CreateAsync(NewTransaction(debit, credit));
        await PostAsync(created.Id);

        var edited = NewTransaction(debit, credit, 5_000);
        edited.Id = created.Id;

        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TransactionService(context).UpdateAsync(edited));

        Assert.Contains("قطعی", ex.Message);
    }

    [Fact]
    public async Task Update_AutoGeneratedDocument_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var auto = NewTransaction(debit, credit);
        auto.ReferenceType = "Invoice";
        auto.ReferenceId = 77;
        var created = await CreateAsync(auto);

        var edited = NewTransaction(debit, credit, 5_000);
        edited.Id = created.Id;

        await using var context = _db.NewContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TransactionService(context).UpdateAsync(edited));

        Assert.Contains("خودکار", ex.Message);
    }

    // ---------------------------------------------------------------
    // قطعی / پیش‌نویس
    // ---------------------------------------------------------------

    [Fact]
    public async Task Post_ThenUnpost_TogglesState_AndRejectsRepeats()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var created = await CreateAsync(NewTransaction(debit, credit));

        await PostAsync(created.Id);
        Assert.True(await IsPostedAsync(created.Id));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new TransactionService(context).PostAsync(created.Id, TenantA));

        await using (var context = _db.NewContext())
            await new TransactionService(context).UnpostAsync(created.Id, TenantA);

        Assert.False(await IsPostedAsync(created.Id));

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new TransactionService(context).UnpostAsync(created.Id, TenantA));
    }

    // ---------------------------------------------------------------
    // حذف
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteManual_DraftDocument_IsSoftDeleted_WithAuditFields()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var created = await CreateAsync(NewTransaction(debit, credit));

        _db.User.UserId = "deleter";
        await using (var context = _db.NewContext())
            await new TransactionService(context).DeleteManualAsync(created.Id, TenantA);

        await using var verify = _db.NewContext();

        Assert.Null(await new TransactionService(verify).GetByIdAsync(created.Id, TenantA));

        var deleted = await verify.SetIncludingDeleted<Transaction>().SingleAsync(t => t.Id == created.Id);
        Assert.True(deleted.IsDeleted);
        Assert.Equal("deleter", deleted.DeletedBy);
        Assert.NotNull(deleted.DeletedAt);
    }

    [Fact]
    public async Task DeleteManual_PostedDocument_Throws()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var created = await CreateAsync(NewTransaction(debit, credit));
        await PostAsync(created.Id);

        await using var context = _db.NewContext();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TransactionService(context).DeleteManualAsync(created.Id, TenantA));
    }

    [Fact]
    public async Task DeleteManual_AutoGeneratedDocument_Throws_ButDeleteAsyncAllowsModules()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var auto = NewTransaction(debit, credit);
        auto.ReferenceType = "Payment";
        auto.ReferenceId = 5;
        var created = await CreateAsync(auto);

        await using (var context = _db.NewContext())
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new TransactionService(context).DeleteManualAsync(created.Id, TenantA));

        // مسیر ماژول‌ها (مثلاً Unpost فاکتور) باید کار کند
        await using (var context = _db.NewContext())
            await new TransactionService(context).DeleteAsync(created.Id, TenantA);

        await using var verify = _db.NewContext();
        Assert.Null(await new TransactionService(verify).GetByIdAsync(created.Id, TenantA));
    }

    [Fact]
    public async Task Operations_OnAnotherTenantsDocument_ReportNotFound()
    {
        var (debit, credit) = await TwoAccountsAsync();
        var created = await CreateAsync(NewTransaction(debit, credit));

        _db.Tenant.TenantId = TenantB;

        await using var context = _db.NewContext();
        var service = new TransactionService(context);

        // حتی اگر شناسه‌ی شرکت اشتباه هم پاس داده شود، Query Filter جلوی دسترسی را می‌گیرد
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PostAsync(created.Id, TenantA));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteManualAsync(created.Id, TenantA));
        Assert.Null(await service.GetByIdAsync(created.Id, TenantA));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<(int Debit, int Credit)> TwoAccountsAsync()
    {
        var debit = await _db.AddAccountAsync(TenantA, "حساب بدهکار");
        var credit = await _db.AddAccountAsync(TenantA, "حساب بستانکار");
        return (debit, credit);
    }

    private static Transaction NewTransaction(int debitAccountId, int creditAccountId, decimal amount = 1_000)
    {
        return new Transaction
        {
            TenantId = TenantA,
            Date = new DateOnly(2026, 3, 21),
            Description = "سند تست",
            Lines =
            {
                new TransactionLine { AccountId = debitAccountId, Debit = amount },
                new TransactionLine { AccountId = creditAccountId, Credit = amount }
            }
        };
    }

    private async Task<Transaction> CreateAsync(Transaction transaction)
    {
        await using var context = _db.NewContext();
        return await new TransactionService(context).CreateAsync(transaction);
    }

    private async Task PostAsync(int id)
    {
        await using var context = _db.NewContext();
        await new TransactionService(context).PostAsync(id, TenantA);
    }

    private async Task<bool> IsPostedAsync(int id)
    {
        await using var context = _db.NewContext();
        var entity = await new TransactionService(context).GetByIdAsync(id, TenantA);
        return entity!.IsPosted;
    }
}