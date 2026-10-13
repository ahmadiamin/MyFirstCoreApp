using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class SystemAccountService : ISystemAccountService
{
    private readonly ApplicationDbContext _context;

    public SystemAccountService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Account> GetOrCreateAsync(int tenantId, string name, AccountType type)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Name == name && a.AccountType == type);

        if (account != null) return account;

        var category = await _context.AccountCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.DefaultAccountType == type);

        if (category == null)
        {
            category = new AccountCategory
            {
                TenantId = tenantId,
                Name = $"دسته‌بندی {name}",
                DefaultAccountType = type,
                IsActive = true
            };
            _context.AccountCategories.Add(category);
            await _context.SaveChangesAsync();
        }

        account = new Account
        {
            TenantId = tenantId,
            Name = name,
            AccountType = type,
            AccountCategoryId = category.Id,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return account;
    }
}
