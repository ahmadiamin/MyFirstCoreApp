using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Tenancy;

namespace MyFirstCoreApp.Tests.TestSupport;

/// <summary>داده‌ی پایه‌ی تست‌های دسترسی: شرکت، کاربر، عضویت، کاتالوگ دسترسی و نقش.</summary>
public sealed class SecurityTestHelper
{
    // کاتالوگ استاندارد تست
    public const string InvoiceView = "Invoice.View";
    public const string InvoiceCreate = "Invoice.Create";
    public const string WarehouseView = "Warehouse.View";
    public const string UserView = "User.View";            // سطح شرکت
    public const string RoleView = "Role.View";            // سطح شرکت
    public const string PermissionView = "Permission.View"; // سراسری
    public const string MenuEdit = "Menu.Edit";            // سراسری
    public const string TenancyCreate = "Tenancy.Create";  // سطح حساب
    public const string Retired = "Retired.View";          // غیرفعال

    private readonly TestDatabase _db;

    public SecurityTestHelper(TestDatabase db) => _db = db;

    public async Task AddTenantAsync(int id, bool active = true)
    {
        await using var context = _db.NewContext();
        context.Tenants.Add(new Tenant { Id = id, Name = $"شرکت {id}", IsActive = active });
        await context.SaveChangesAsync();
    }

    public async Task AddUserAsync(string id, bool active = true, string? fullName = null)
    {
        await using var context = _db.NewContext();
        context.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"{id}@example.com",
            NormalizedUserName = $"{id}@EXAMPLE.COM".ToUpperInvariant(),
            Email = $"{id}@example.com",
            NormalizedEmail = $"{id}@EXAMPLE.COM".ToUpperInvariant(),
            FullName = fullName ?? $"کاربر {id}",
            IsActive = active
        });
        await context.SaveChangesAsync();
    }

    public async Task AddMembershipAsync(string userId, int tenantId, bool owner = false, string? first = null, string? last = null)
    {
        await using var context = _db.NewContext();
        context.UserTenants.Add(new UserTenant
        {
            UserId = userId,
            TenantId = tenantId,
            IsOwner = owner,
            FirstName = first,
            LastName = last
        });
        await context.SaveChangesAsync();
    }

    public async Task SeedPermissionsAsync()
    {
        await using var context = _db.NewContext();

        foreach (var (name, group, active) in new[]
                 {
                     (InvoiceView, "فاکتور", true), (InvoiceCreate, "فاکتور", true), (WarehouseView, "انبار", true),
                     (UserView, "کاربران", true), (RoleView, "نقش‌ها", true),
                     (PermissionView, "سیستم", true), (MenuEdit, "سیستم", true),
                     (TenancyCreate, "شرکت", true), (Retired, "قدیمی", false)
                 })
        {
            context.Permissions.Add(new Permission { Name = name, DisplayName = name, GroupName = group, IsActive = active });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>نقش شرکت را مستقیم (بدون اعتبارسنجی سرویس) می‌سازد؛ برای چیدن حالت‌های خاص.</summary>
    public async Task<int> AddRoleAsync(int tenantId, string name, bool active = true, params string[] permissions)
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenantId;

        try
        {
            await using var context = _db.NewContext();
            var role = new TenantRole { TenantId = tenantId, Name = name, IsActive = active };

            foreach (var permission in permissions)
                role.Permissions.Add(new TenantRolePermission { TenantId = tenantId, PermissionName = permission });

            context.TenantRoles.Add(role);
            await context.SaveChangesAsync();
            return role.Id;
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }

    public async Task AssignAsync(string userId, int tenantId, int roleId)
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenantId;

        try
        {
            await using var context = _db.NewContext();
            context.UserTenantRoles.Add(new UserTenantRole { TenantId = tenantId, UserId = userId, TenantRoleId = roleId });
            await context.SaveChangesAsync();
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }
}