extern alias StimulsoftBase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Authorization;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Persistence.Seed;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Logging;
using MyFirstCoreApp.Services;
using MyFirstCoreApp.Services.CurrentUser;
using MyFirstCoreApp.Services.Interfaces;
using MyFirstCoreApp.Settings;
using Serilog;
using Serilog.Events;
using System.Threading.RateLimiting;

// لاگر اولیه: خطاهای زمان راه‌اندازی (قبل از خواندن تنظیمات) هم ثبت شوند
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // لاگ ساخت‌یافته: تنظیمات از بخش "Serilog" در appsettings می‌آید (سطح‌ها، Sinkها، نگه‌داری فایل)
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    {
        options.ValidationInterval = TimeSpan.Zero;
    });

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    });

    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IRoleService, RoleService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<IMenuService, MenuService>();
    builder.Services.AddScoped<IFileService, FileService>();
    builder.Services.AddScoped<ICurrentTenant, CurrentTenantService>();
    builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
    builder.Services.AddScoped<ITransactionService, TransactionService>();
    builder.Services.AddScoped<IInvoiceService, InvoiceService>();
    builder.Services.AddScoped<IChequeService, ChequeService>();
    builder.Services.AddScoped<IPaymentService, PaymentService>();

    builder.Services.AddControllersWithViews();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("RegistrationSms", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 6,
                    Window = TimeSpan.FromMinutes(10),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

    builder.Services
        .AddDefaultIdentity<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

    builder.Services.AddScoped<IUserConfirmation<ApplicationUser>, ApplicationUserConfirmation>();

    builder.Services.Configure<EmailSettings>(
        builder.Configuration.GetSection("EmailSettings"));

    builder.Services.AddScoped<IEmailSender, EmailSender>();
    builder.Services.AddScoped<ITenantService, TenantService>();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(2);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    StimulsoftBase::Stimulsoft.Base.StiLicense.Key = "6vJhGtLLLz2GNviWmUTrhSqnOItdDwjBylQzQcAOiHkO46nMQvol4ASeg91in+mGJLnn2KMIpg3eSXQSgaFOm15+0l" +
    "hekKip+wRGMwXsKpHAkTvorOFqnpF9rchcYoxHXtjNDLiDHZGTIWq6D/2q4k/eiJm9fV6FdaJIUbWGS3whFWRLPHWC" +
    "BsWnalqTdZlP9knjaWclfjmUKf2Ksc5btMD6pmR7ZHQfHXfdgYK7tLR1rqtxYxBzOPq3LIBvd3spkQhKb07LTZQoyQ" +
    "3vmRSMALmJSS6ovIS59XPS+oSm8wgvuRFqE1im111GROa7Ww3tNJTA45lkbXX+SocdwXvEZyaaq61Uc1dBg+4uFRxv" +
    "yRWvX5WDmJz1X0VLIbHpcIjdEDJUvVAN7Z+FW5xKsV5ySPs8aegsY9ndn4DmoZ1kWvzUaz+E1mxMbOd3tyaNnmVhPZ" +
    "eIBILmKJGN0BwnnI5fu6JHMM/9QR2tMO1Z4pIwae4P92gKBrt0MqhvnU1Q6kIaPPuG2XBIvAWykVeH2a9EP6064e11" +
    "PFCBX4gEpJ3XFD0peE5+ddZh+h495qUc1H2B";

    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // خلاصه‌ی هر درخواست (روش، مسیر، وضعیت، زمان، شرکت، کاربر). قبل از Session/Auth تا کل زمان درخواست را بسنجد
    app.UseAppRequestLogging();

    app.UseSession();
    app.UseRouting();
    app.UseRateLimiter();

    app.UseAuthentication();

    // شرکت فعال، کاربر و TraceId را به همه‌ی لاگ‌های این درخواست اضافه می‌کند (بعد از Session و Authentication)
    app.UseMiddleware<LogContextMiddleware>();

    app.UseAuthorization();

    app.MapStaticAssets();

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}");

    app.MapRazorPages()
       .WithStaticAssets();

    // ابزار dotnet ef برنامه را فقط برای ساخت مدل بالا می‌آورد؛ در آن حالت به دیتابیس دست نمی‌زنیم
    if (!EF.IsDesignTime)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var isDevelopment = app.Environment.IsDevelopment();

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Migration خودکار: پیش‌فرض فقط در توسعه (قابل تغییر با Database:MigrateOnStartup).
            // در محیط واقعی Migration عمداً دستی اجرا می‌شود (قابل بازبینی، قبل از آن Backup)
            var migrateOnStartup = app.Configuration.GetValue<bool?>("Database:MigrateOnStartup") ?? isDevelopment;

            if (migrateOnStartup)
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                // دیتابیسِ قدیمی‌تر از کد = خطاهای عجیب در حین کار؛ همین‌جا و با پیام واضح متوقف شو
                var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

                if (pending.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"{pending.Count} Migration اعمال‌نشده وجود دارد ({string.Join(", ", pending)}). " +
                        "ابتدا از دیتابیس Backup بگیر و «dotnet ef database update» را اجرا کن، " +
                        "یا موقتاً Database:MigrateOnStartup=true بگذار.");
                }
            }

            await IdentitySeeder.SeedAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "خطا در آماده‌سازی دیتابیس یا Seed اولیه");

            // محیط واقعی: با دیتابیس ناقص یا Seed ناقص (مثلاً بدون نقش‌ها و دسترسی‌ها) بالا نیا
            if (!isDevelopment)
                throw;
        }
    }

    app.Run();
    return 0;
}
catch (HostAbortedException)
{
    // ابزار dotnet ef برنامه را موقتاً بالا می‌آورد و متوقف می‌کند؛ خطا نیست
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "برنامه به‌صورت غیرمنتظره متوقف شد");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
