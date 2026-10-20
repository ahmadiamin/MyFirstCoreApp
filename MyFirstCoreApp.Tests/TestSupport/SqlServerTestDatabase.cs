using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Persistence.Interceptors;
using MyFirstCoreApp.Infrastructure.Persistence.Sql;
using Xunit;

namespace MyFirstCoreApp.Tests.TestSupport;

/// <summary>
/// دیتابیس موقتِ واقعی SQL Server برای تست Stored Procedureها (T-SQL روی SQLite اجرا نمی‌شود).
///
/// اجرا: متغیر محیطی MYFIRSTCOREAPP_TEST_SQLSERVER را با Connection String «سرور» (بدون نام دیتابیس) تنظیم کن، مثلاً
///   Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true
/// اگر تنظیم نباشد، تست‌های [SqlServerFact] «Skip» می‌شوند (و سبز/قرمز نمی‌شوند).
///
/// هر نمونه یک دیتابیس تازه می‌سازد (EnsureCreated + نصب پروسیجرها از همان فایل‌های .sql) و در پایان حذفش می‌کند.
/// </summary>
public sealed class SqlServerTestDatabase : IDisposable
{
    public const string EnvironmentVariable = "MYFIRSTCOREAPP_TEST_SQLSERVER";

    private readonly string _serverConnectionString;
    private readonly string _databaseName;
    private readonly string _connectionString;

    public TestDatabase.FakeTenant Tenant { get; } = new();
    public TestDatabase.FakeUser User { get; } = new();

    public SqlServerTestDatabase()
    {
        _serverConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariable)
            ?? throw new InvalidOperationException($"متغیر محیطی {EnvironmentVariable} تنظیم نشده است.");

        _databaseName = "MFCA_ReportTests_" + Guid.NewGuid().ToString("N");
        _connectionString = new SqlConnectionStringBuilder(_serverConnectionString)
        {
            InitialCatalog = _databaseName
        }.ConnectionString;

        using (var context = NewContext())
        {
            context.Database.EnsureCreated();
        }

        InstallProcedures();
    }

    public ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(User, Tenant))
            .Options;

        return new ApplicationDbContext(options, Tenant);
    }

    public async Task<int> AddAccountAsync(int tenantId, string name, AccountType type)
    {
        var previous = Tenant.TenantId;
        Tenant.TenantId = tenantId;

        try
        {
            await using var context = NewContext();

            var category = new MyFirstCoreApp.Domain.Entities.Accounting.AccountCategory
            {
                Name = "دسته " + name,
                DefaultAccountType = type,
                IsActive = true
            };
            context.AccountCategories.Add(category);
            await context.SaveChangesAsync();

            var account = new MyFirstCoreApp.Domain.Entities.Accounting.Account
            {
                Name = name,
                AccountType = type,
                AccountCategoryId = category.Id,
                IsActive = true
            };
            context.Accounts.Add(account);
            await context.SaveChangesAsync();

            return account.Id;
        }
        finally
        {
            Tenant.TenantId = previous;
        }
    }

    private void InstallProcedures()
    {
        using var context = NewContext();
        var connection = context.Database.GetDbConnection();
        connection.Open();

        foreach (var name in ReportSqlScripts.ProcedureNames)
        {
            using var command = connection.CreateCommand();
            command.CommandText = ReportSqlScripts.Read(name);
            command.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        SqlConnection.ClearAllPools();

        using var connection = new SqlConnection(_serverConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE [{_databaseName}];";
        command.ExecuteNonQuery();
    }
}

/// <summary>[Fact] که بدون متغیر محیطی سرور SQL، Skip می‌شود.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerTestDatabase.EnvironmentVariable)))
        {
            Skip = $"برای اجرای این تست، متغیر محیطی {SqlServerTestDatabase.EnvironmentVariable} را " +
                   "با Connection String سرور SQL (بدون نام دیتابیس) تنظیم کنید.";
        }
    }
}
