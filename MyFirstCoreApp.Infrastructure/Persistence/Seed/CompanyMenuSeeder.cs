using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Infrastructure.Persistence.Seed;

/// <summary>
/// منوهای مدیریت شرکت (کارکنان، سمت‌ها، دفترچه‌ی تلفن). مستقل از IdentitySeeder و idempotent:
/// گروه والد را خودش پیدا می‌کند یا می‌سازد و هر آیتم را فقط اگر نباشد اضافه می‌کند (بر اساس Url).
/// از Program.cs بعد از IdentitySeeder صدا زده می‌شود.
/// </summary>
public static class CompanyMenuSeeder
{
    private const string ParentTitle = "مدیریت شرکت";

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var parent = await context.Menus
            .FirstOrDefaultAsync(m => m.ParentId == null && m.Title == ParentTitle);

        if (parent == null)
        {
            parent = new Menu
            {
                Title = ParentTitle,
                Icon = "fa-solid fa-building",
                Url = null,
                Permission = null,
                ParentId = null,
                SortOrder = 90,
                IsActive = true
            };

            context.Menus.Add(parent);
            await context.SaveChangesAsync();
        }

        var children = new[]
        {
            new Menu { Title = "کارکنان", Url = "/Company/Staff", Icon = "fa-solid fa-users", Permission = "User.View", SortOrder = 1 },
            new Menu { Title = "سمت‌ها و دسترسی‌ها", Url = "/Company/Positions", Icon = "fa-solid fa-user-shield", Permission = "Role.View", SortOrder = 2 },
            // بدون دسترسی: هر عضو شرکت (و حتی بدون سمت) دفترچه‌ی تلفن داخلی را می‌بیند
            new Menu { Title = "دفترچه‌ی تلفن داخلی", Url = "/Company/Staff/Directory", Icon = "fa-solid fa-phone", Permission = null, SortOrder = 3 }
        };

        foreach (var child in children)
        {
            var exists = await context.Menus.AnyAsync(m => m.Url == child.Url);
            if (exists) continue;

            child.ParentId = parent.Id;
            child.IsActive = true;
            context.Menus.Add(child);
        }

        await context.SaveChangesAsync();
    }
}
