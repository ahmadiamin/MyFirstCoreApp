using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Persistence.Interceptors;

namespace MyFirstCoreApp.Tests.TestSupport;

public sealed class PasswordResetSqlServerTestDatabase
{
    private readonly string _connectionString;

    public TestDatabase.FakeTenant Tenant { get; } = new();
    public TestDatabase.FakeUser User { get; } = new();

    public PasswordResetSqlServerTestDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        _connectionString =
            configuration.GetConnectionString("TestConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:TestConnection is not configured.");

        var builder = new SqlConnectionStringBuilder(_connectionString);

        if (!string.Equals(
                builder.InitialCatalog,
                "MyFirstCoreAppDbTests",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Password reset integration tests must use MyFirstCoreAppDbTests.");
        }
    }

    public ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(User, Tenant))
            .Options;

        return new ApplicationDbContext(options, Tenant);
    }
}