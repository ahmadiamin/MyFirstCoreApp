using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Persistence.Interceptors;
using MyFirstCoreApp.Infrastructure.Services;

namespace MyFirstCoreApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ساعت ایران (برای «امروز» در تاریخ سند، بازه‌ی پیش‌فرض گزارش و ...)
        services.AddSingleton<TimeProvider, IranTimeProvider>();
        services.AddDataProtection();
        services.AddHttpClient<IFarazSmsService, FarazSmsService>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<ITenantSmsSettingsService, TenantSmsSettingsService>();
        services.AddHttpClient<ISystemSmsService, SystemSmsService>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHostedService<ChequeReminderHostedService>();

        // Audit + TenantId + حذف نرم در یک نقطه.
        // Scoped است چون به ICurrentUser و ICurrentTenant (وابسته به درخواست) نیاز دارد؛
        // پس باید با overload (serviceProvider, options) به DbContext وصل شود
        // و DbContext نباید با AddDbContextPool ثبت شود.
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

            options.AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        // حساب‌های سیستمی (بانک، دریافتنی، پرداختنی، ...)
        services.AddScoped<ISystemAccountService, SystemAccountService>();

        // سال‌های مالی
        services.AddScoped<IFiscalYearService, FiscalYearService>();

        // گزارش‌ها (Stored Procedure + Dapper)
        services.AddScoped<IReportService, ReportService>();

        // داشبورد (Stored Procedure + Dapper)
        services.AddScoped<IDashboardService, DashboardService>();

        // پیوست عمومی (فایل‌ها بیرون از wwwroot) و قرض
        services.Configure<AttachmentOptions>(options =>
        {
            var section = configuration.GetSection("Attachments");

            if (!string.IsNullOrWhiteSpace(section["RootPath"]))
                options.RootPath = section["RootPath"]!;

            if (long.TryParse(section["MaxFileSizeBytes"], out var maxSize) && maxSize > 0)
                options.MaxFileSizeBytes = maxSize;

            if (int.TryParse(section["MaxFilesPerEntity"], out var maxFiles) && maxFiles > 0)
                options.MaxFilesPerEntity = maxFiles;
        });

        services.AddSingleton<IAttachmentStore, LocalAttachmentStore>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ILoanService, LoanService>();

        services.AddScoped<IStockBalanceService, StockBalanceService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<IInventoryAnalyticsService, InventoryAnalyticsService>();
        services.AddScoped<ICustomerPortalService, CustomerPortalService>();
        services.AddScoped<IWarehouseService, WarehouseService>();

        return services;
    }
}
