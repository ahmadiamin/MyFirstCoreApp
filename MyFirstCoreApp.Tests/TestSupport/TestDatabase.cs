using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Persistence.Interceptors;

namespace MyFirstCoreApp.Tests.TestSupport;

/// <summary>
/// دیتابیس SQLite درون‌حافظه با همان DbContext و Interceptor برنامه.
/// هر درخواست واقعی یک Context تازه دارد؛ تست‌ها هم با NewContext() همین را شبیه‌سازی می‌کنند.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public FakeTenant Tenant { get; } = new();
    public FakeUser User { get; } = new();

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(User, Tenant))
            .Options;

        return new ApplicationDbContext(options, Tenant);
    }

    /// <summary>یک حساب برای شرکت مشخص می‌سازد و شناسه‌اش را برمی‌گرداند.</summary>
    public async Task<int> AddAccountAsync(int tenantId, string name = "حساب تست")
    {
        var previousTenant = Tenant.TenantId;
        Tenant.TenantId = tenantId;

        try
        {
            await using var context = NewContext();

            var category = new AccountCategory
            {
                Name = "دسته‌بندی تست",
                DefaultAccountType = AccountType.Bank,
                IsActive = true
            };
            context.AccountCategories.Add(category);
            await context.SaveChangesAsync();

            var account = new Account
            {
                Name = name,
                AccountType = AccountType.Bank,
                AccountCategoryId = category.Id,
                IsActive = true
            };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            return account.Id;
        }
        finally
        {
            Tenant.TenantId = previousTenant;
        }
    }

    public void Dispose() => _connection.Dispose();

    public sealed class FakeTenant : ICurrentTenant
    {
        public int? TenantId { get; set; }
        public string? TenantName => null;
        public bool IsSet => TenantId.HasValue;
    }

    public sealed class FakeUser : ICurrentUser
    {
        public string? UserId { get; set; }
        public string? UserName => UserId;
        public bool IsAuthenticated => UserId != null;
    }
}
