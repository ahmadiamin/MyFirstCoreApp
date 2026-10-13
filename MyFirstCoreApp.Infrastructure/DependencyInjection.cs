using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }
}
