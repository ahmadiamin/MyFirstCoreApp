using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure;

public sealed class DesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // برای اجرای Migration بدون خواندن فایل‌های تنظیمات محلی، این مقدار را فقط
        // در محیط همان فرایند تنظیم کنید. در صورت نبود آن، رفتار پیشین حفظ می‌شود.
        var connectionString = Environment.GetEnvironmentVariable("MYFIRSTCOREAPP_MIGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root is not null &&
                   !File.Exists(Path.Combine(root.FullName, "MyFirstCoreApp", "appsettings.json")))
                root = root.Parent;

            if (root is null)
                throw new InvalidOperationException("Application settings were not found.");

            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? "Production";

            var configuration = new ConfigurationBuilder()
                .SetBasePath(root.FullName)
                .AddJsonFile(Path.Combine("MyFirstCoreApp", "appsettings.json"), optional: false)
                .AddJsonFile(
                    Path.Combine("MyFirstCoreApp", $"appsettings.{environmentName}.json"),
                    optional: true)
                .AddEnvironmentVariables()
                .Build();

            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Database configuration is missing.");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options, new DesignTimeTenant());
    }

    private sealed class DesignTimeTenant : ICurrentTenant
    {
        public int? TenantId => null;
        public string? TenantName => null;
        public bool IsSet => false;
    }
}
