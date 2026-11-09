using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// سال مالیِ سند «الزامی» است: دیتابیس نباید هیچ سندی را بدون سال مالیِ معتبر بپذیرد
/// (حتی اگر کسی سرویس را دور بزند).
/// </summary>
public class FiscalYearRequiredTests : IDisposable
{
    private const int Tenant = 1;

    private readonly TestDatabase _db = new();

    public FiscalYearRequiredTests()
    {
        _db.Tenant.TenantId = Tenant;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ATransaction_WithoutAFiscalYear_CannotBeSaved()
    {
        var debit = await _db.AddAccountAsync(Tenant, "بدهکار");
        var credit = await _db.AddAccountAsync(Tenant, "بستانکار");

        await using var context = _db.NewContext();
        context.Transactions.Add(NewTransaction(debit, credit, fiscalYearId: 0));

        // سال مالی 0 وجود ندارد ← نقض کلید خارجی
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ATransaction_WithARealFiscalYear_IsSaved()
    {
        var debit = await _db.AddAccountAsync(Tenant, "بدهکار");
        var credit = await _db.AddAccountAsync(Tenant, "بستانکار");

        int yearId;
        await using (var context = _db.NewContext())
            yearId = (await context.FiscalYears.SingleAsync()).Id;

        await using (var context = _db.NewContext())
        {
            context.Transactions.Add(NewTransaction(debit, credit, yearId));
            await context.SaveChangesAsync();
        }

        await using var verify = _db.NewContext();
        Assert.Equal(yearId, (await verify.Transactions.SingleAsync()).FiscalYearId);
    }

    [Fact]
    public async Task EveryDocumentMadeByTheService_GetsItsFiscalYear()
    {
        var debit = await _db.AddAccountAsync(Tenant, "بدهکار");
        var credit = await _db.AddAccountAsync(Tenant, "بستانکار");

        await using var context = _db.NewContext();
        var created = await new TransactionService(context).CreateAsync(NewTransaction(debit, credit, fiscalYearId: 0));

        Assert.NotEqual(0, created.FiscalYearId);
    }

    [Fact]
    public async Task AFiscalYear_WithDocuments_CannotBeRemovedFromTheDatabase()
    {
        var debit = await _db.AddAccountAsync(Tenant, "بدهکار");
        var credit = await _db.AddAccountAsync(Tenant, "بستانکار");

        await using (var context = _db.NewContext())
            await new TransactionService(context).CreateAsync(NewTransaction(debit, credit, fiscalYearId: 0));

        // حتی حذف «واقعی» مستقیم (بدون حذف نرم) باید به‌خاطر کلید خارجی رد شود
        await using var raw = _db.NewContext();
        await Assert.ThrowsAnyAsync<Exception>(
            () => raw.Database.ExecuteSqlRawAsync("DELETE FROM FiscalYears"));
    }

    private static Transaction NewTransaction(int debit, int credit, int fiscalYearId) => new()
    {
        TenantId = Tenant,
        FiscalYearId = fiscalYearId,
        Date = new DateOnly(2026, 3, 22),
        Description = "تست",
        TotalAmount = 100,
        Lines =
        {
            new TransactionLine { TenantId = Tenant, LineNumber = 1, AccountId = debit, Debit = 100 },
            new TransactionLine { TenantId = Tenant, LineNumber = 2, AccountId = credit, Credit = 100 }
        }
    };
}