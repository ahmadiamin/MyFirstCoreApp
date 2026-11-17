using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;
using static MyFirstCoreApp.Tests.TestSupport.SecurityTestHelper;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// نقطه‌ی واحد تصمیم‌گیری دسترسی: مدیر سیستم / سطح حساب / سراسری / سطح شرکت،
/// از جمله سناریوی «کارشناس فروش فقط فاکتور ثبت کند» و «انباردار فقط منوهای انبار».
/// </summary>
public class PermissionEvaluatorTests : IDisposable
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;

    public PermissionEvaluatorTests()
    {
        _data = new SecurityTestHelper(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task ArrangeAsync()
    {
        await _data.SeedPermissionsAsync();
        await _data.AddTenantAsync(CompanyA);
        await _data.AddTenantAsync(CompanyB);

        await _data.AddUserAsync("owner");
        await _data.AddUserAsync("sales");
        await _data.AddUserAsync("storekeeper");

        await _data.AddMembershipAsync("owner", CompanyA, owner: true);
        await _data.AddMembershipAsync("sales", CompanyA);
        await _data.AddMembershipAsync("storekeeper", CompanyA);

        // «کارشناس فروش»: فقط ثبت و دیدن فاکتور
        var salesRole = await _data.AddRoleAsync(CompanyA, "کارشناس فروش", true, InvoiceCreate, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, salesRole);

        // «انباردار»: فقط انبار
        var storeRole = await _data.AddRoleAsync(CompanyA, "انباردار", true, WarehouseView);
        await _data.AssignAsync("storekeeper", CompanyA, storeRole);
    }

    // ---------------------------------------------------------------
    // سناریوهای شرکت
    // ---------------------------------------------------------------

    [Fact]
    public async Task SalesExpert_CanRegisterInvoices_ButNothingElse()
    {
        await ArrangeAsync();
        var sales = Subject("sales", CompanyA);

        Assert.True(await HasAsync(sales, InvoiceCreate));
        Assert.True(await HasAsync(sales, InvoiceView));

        Assert.False(await HasAsync(sales, WarehouseView));
        Assert.False(await HasAsync(sales, UserView));        // مدیریت کاربران
        Assert.False(await HasAsync(sales, RoleView));
    }

    [Fact]
    public async Task Storekeeper_SeesOnlyWarehouse()
    {
        await ArrangeAsync();
        var storekeeper = Subject("storekeeper", CompanyA);

        Assert.True(await HasAsync(storekeeper, WarehouseView));
        Assert.False(await HasAsync(storekeeper, InvoiceView));
        Assert.False(await HasAsync(storekeeper, InvoiceCreate));
    }

    [Fact]
    public async Task Owner_HasEverythingInTheCompany_IncludingUserAndRoleManagement()
    {
        await ArrangeAsync();
        var owner = Subject("owner", CompanyA);

        foreach (var permission in new[] { InvoiceView, InvoiceCreate, WarehouseView, UserView, RoleView })
            Assert.True(await HasAsync(owner, permission), permission);
    }

    [Fact]
    public async Task SomeoneWhoIsNotAMemberOfTheActiveCompany_HasNothing()
    {
        await ArrangeAsync();

        // «sales» عضو شرکت A است ولی شرکت فعالش B است
        var sales = Subject("sales", CompanyB);

        Assert.False(await HasAsync(sales, InvoiceView));
        Assert.False(await HasAsync(sales, InvoiceCreate));
    }

    [Fact]
    public async Task CompanyPermissions_RequireAnActiveCompany_EvenForTheOwner()
    {
        await ArrangeAsync();
        var noCompany = new PermissionSubject(true, false, "owner", null);

        Assert.False(await HasAsync(noCompany, InvoiceView));
        Assert.False(await HasAsync(noCompany, UserView));
    }

    // ---------------------------------------------------------------
    // مدیر سیستم، حساب، سراسری
    // ---------------------------------------------------------------

    [Fact]
    public async Task Anonymous_IsAlwaysDenied()
    {
        await ArrangeAsync();

        var anonymous = new PermissionSubject(false, false, null, CompanyA);

        Assert.False(await HasAsync(anonymous, InvoiceView));
        Assert.False(await HasAsync(anonymous, TenancyCreate));
        Assert.False(await HasAsync(anonymous, PermissionView));
    }

    [Fact]
    public async Task SuperAdmin_PassesEverything()
    {
        await ArrangeAsync();
        var admin = new PermissionSubject(true, true, "someone", null);

        foreach (var permission in new[] { InvoiceView, UserView, PermissionView, MenuEdit, TenancyCreate })
            Assert.True(await HasAsync(admin, permission), permission);
    }

    [Fact]
    public async Task AccountLevelPermissions_AreForEveryAuthenticatedUser()
    {
        await ArrangeAsync();

        // شرکتی ندارد و هیچ نقشی هم ندارد؛ ولی می‌تواند شرکت بسازد
        var newcomer = new PermissionSubject(true, false, "brand-new-user", null);

        Assert.True(await HasAsync(newcomer, TenancyCreate));
        Assert.False(await HasAsync(newcomer, InvoiceView));
    }

    [Fact]
    public async Task PlatformPermissions_OnlyThroughGlobalRoles_NeverThroughOwnershipOrCompanyRoles()
    {
        await ArrangeAsync();

        // مالک شرکت و کارشناس فروش: هیچ‌کدام مدیر سیستم نیستند
        Assert.False(await HasAsync(Subject("owner", CompanyA), PermissionView));
        Assert.False(await HasAsync(Subject("owner", CompanyA), MenuEdit));
        Assert.False(await HasAsync(Subject("sales", CompanyA), PermissionView));

        // کاربری با «نقش سراسری» که دسترسی سراسری دارد
        await GrantGlobalRoleAsync("sales", "PlatformEditor", PermissionView);

        Assert.True(await HasAsync(Subject("sales", CompanyA), PermissionView));
        Assert.False(await HasAsync(Subject("sales", CompanyA), MenuEdit));      // فقط همان یکی
    }

    [Fact]
    public async Task GlobalRoles_NoLongerGrantCompanyPermissions()
    {
        await ArrangeAsync();

        // کاربری که نقش سراسری با «دسترسی سطح شرکت» دارد (مثل مدل قدیمی Admin)
        await GrantGlobalRoleAsync("storekeeper", "Admin", InvoiceView, UserView);

        var storekeeper = Subject("storekeeper", CompanyA);

        // دسترسی شرکت فقط از نقش‌های «همین شرکت» می‌آید
        Assert.False(await HasAsync(storekeeper, InvoiceView));
        Assert.False(await HasAsync(storekeeper, UserView));
        Assert.True(await HasAsync(storekeeper, WarehouseView));
    }

    // ---------------------------------------------------------------
    // GetAllowed (منو و داشبورد)
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllowed_ReturnsTheAllowedSubset_AndIgnoresEmptyEntries()
    {
        await ArrangeAsync();

        await using var context = _db.NewContext();
        var evaluator = NewEvaluator(context);

        var allowed = await evaluator.GetAllowedAsync(
            Subject("sales", CompanyA),
            new string?[] { InvoiceView, WarehouseView, TenancyCreate, PermissionView, null, "", "  " });

        // فاکتور (نقش) + سطح حساب؛ نه انبار، نه سراسری؛ مقدارهای خالی در نتیجه نمی‌آیند
        Assert.Equal(new[] { InvoiceView, TenancyCreate }.OrderBy(x => x), allowed.OrderBy(x => x));
    }

    [Fact]
    public async Task HasAsync_WithAnEmptyPermission_MeansJustAuthenticated()
    {
        await ArrangeAsync();

        await using var context = _db.NewContext();
        var evaluator = NewEvaluator(context);

        Assert.True(await evaluator.HasAsync(Subject("sales", CompanyA), ""));
        Assert.False(await evaluator.HasAsync(new PermissionSubject(false, false, null, null), ""));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static PermissionSubject Subject(string userId, int tenantId) => new(true, false, userId, tenantId);

    private async Task<bool> HasAsync(PermissionSubject subject, string permission)
    {
        await using var context = _db.NewContext();
        return await NewEvaluator(context).HasAsync(subject, permission);
    }

    private static PermissionEvaluator NewEvaluator(MyFirstCoreApp.Infrastructure.Persistence.ApplicationDbContext context)
        => new(new TenantAccessService(context), new PlatformPermissionChecker(context));

    /// <summary>نقش سراسریِ Identity (مثل مدل قدیمی) با دسترسی‌های داده‌شده.</summary>
    private async Task GrantGlobalRoleAsync(string userId, string roleName, params string[] permissionNames)
    {
        await using var context = _db.NewContext();

        var role = new IdentityRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() };
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        context.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = role.Id });

        foreach (var name in permissionNames)
        {
            var permission = await context.Permissions.SingleAsync(p => p.Name == name);
            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        await context.SaveChangesAsync();
    }
}
