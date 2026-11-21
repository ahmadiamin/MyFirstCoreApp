using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Projects;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class ProjectExpenseServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private const int TenantId = 1;
    public ProjectExpenseServiceTests() => _database.Tenant.TenantId = TenantId;
    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task CreatesPostedExpenseAndAllowsPartialPaymentWithoutOverpaying()
    {
        int cashId;
        await using (var db = _database.NewContext())
        {
            var cashCategory = new AccountCategory { TenantId = TenantId, Name = "Cash", DefaultAccountType = AccountType.Cash, IsActive = true };
            db.AccountCategories.Add(cashCategory);
            await db.SaveChangesAsync();
            var cash = new Account { TenantId = TenantId, Name = "صندوق", AccountType = AccountType.Cash, AccountCategoryId = cashCategory.Id, IsActive = true };
            db.Accounts.Add(cash);
            await db.SaveChangesAsync();
            cashId = cash.Id;
        }

        await using var context = _database.NewContext();
        var service = new ProjectExpenseService(context, new TransactionService(context), new SystemAccountService(context));
        var id = await service.CreateAsync(new ProjectExpenseRequest { Date = new(2026, 11, 21), ExpenseAccountId = 0, Amount = 1000, Description = "اجاره دفتر" }, TenantId);
        var expenseDto = await service.GetByIdAsync(id, TenantId);
        Assert.NotNull(expenseDto);
        Assert.Equal(1000, expenseDto.Amount);
        Assert.Equal("هزینه‌های عمومی", expenseDto.ExpenseAccountName);
        Assert.Single(await service.GetListAsync(TenantId));
        Assert.Equal(1000, await context.Transactions.Where(x => x.ReferenceType == "ProjectExpense" && x.IsPosted).SumAsync(x => x.TotalAmount));

        await service.AddPaymentAsync(new ProjectExpensePaymentRequest { ProjectExpenseId = id, Date = new(2026, 11, 21), Amount = 400, AccountId = cashId, PaymentMethod = PaymentMethod.Cash }, TenantId);
        expenseDto = await service.GetByIdAsync(id, TenantId);
        Assert.NotNull(expenseDto);
        Assert.Equal(400, expenseDto.PaidAmount);
        Assert.Equal(600, expenseDto.RemainingAmount);
        Assert.Single(expenseDto.Payments);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddPaymentAsync(new ProjectExpensePaymentRequest
        { ProjectExpenseId = id, Date = new(2026, 11, 21), Amount = 601, AccountId = cashId, PaymentMethod = PaymentMethod.Cash }, TenantId));

        var paymentId = expenseDto.Payments.Single().Id;
        await service.DeletePaymentAsync(id, paymentId, TenantId);
        expenseDto = await service.GetByIdAsync(id, TenantId);
        Assert.NotNull(expenseDto);
        Assert.Equal(0, expenseDto.PaidAmount);
        Assert.Empty(expenseDto.Payments);

        await using var deleteContext = _database.NewContext();
        var deleteService = new ProjectExpenseService(deleteContext, new TransactionService(deleteContext), new SystemAccountService(deleteContext));
        await deleteService.DeleteAsync(id, TenantId);
        Assert.Null(await deleteService.GetByIdAsync(id, TenantId));
        Assert.All(await deleteContext.Transactions.IgnoreQueryFilters().Where(x => x.ReferenceType == "ProjectExpense" || x.ReferenceType == "ProjectExpensePayment").ToListAsync(),
            transaction => Assert.True(transaction.IsDeleted));
    }
}
