using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;
using static MyFirstCoreApp.Tests.TestSupport.SecurityTestHelper;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>«این کاربر در این شرکت چه دسترسی‌هایی دارد؟»؛ پایه‌ی همه‌ی تصمیم‌های دسترسی سطح شرکت.</summary>
public class TenantAccessServiceTests : IDisposable
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;

    public TenantAccessServiceTests()
    {
        _data = new SecurityTestHelper(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task ArrangeBaseAsync()
    {
        await _data.SeedPermissionsAsync();
        await _data.AddTenantAsync(CompanyA);
        await _data.AddTenantAsync(CompanyB);
        await _data.AddUserAsync("owner");
        await _data.AddUserAsync("sales");
    }

    [Fact]
    public async Task Owner_GetsEveryActiveTenantScopePermission_ButNeverPlatformOrRetiredOnes()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("owner", CompanyA, owner: true);

        var permissions = await PermissionsAsync("owner", CompanyA);

        Assert.Equal(
            new[] { InvoiceCreate, InvoiceView, RoleView, UserView, WarehouseView }.OrderBy(x => x),
            permissions.OrderBy(x => x));

        Assert.DoesNotContain(PermissionView, permissions);     // سراسری
        Assert.DoesNotContain(MenuEdit, permissions);           // سراسری
        Assert.DoesNotContain(TenancyCreate, permissions);      // سطح حساب (نیازی به نقش ندارد)
        Assert.DoesNotContain(Retired, permissions);            // غیرفعال
    }

    [Fact]
    public async Task Member_GetsOnlyThePermissionsOfHisRolesInThisCompany()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);

        var role = await _data.AddRoleAsync(CompanyA, "کارشناس فروش", true, InvoiceCreate, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, role);

        var permissions = await PermissionsAsync("sales", CompanyA);

        Assert.Equal(new[] { InvoiceCreate, InvoiceView }, permissions.OrderBy(x => x));
        Assert.DoesNotContain(WarehouseView, permissions);
        Assert.DoesNotContain(UserView, permissions);
    }

    [Fact]
    public async Task SeveralRoles_AreUnioned()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);

        var sales = await _data.AddRoleAsync(CompanyA, "کارشناس فروش", true, InvoiceView);
        var warehouse = await _data.AddRoleAsync(CompanyA, "انباردار", true, WarehouseView, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, sales);
        await _data.AssignAsync("sales", CompanyA, warehouse);

        Assert.Equal(new[] { InvoiceView, WarehouseView }, (await PermissionsAsync("sales", CompanyA)).OrderBy(x => x));
    }

    [Fact]
    public async Task ARoleOfAnotherCompany_NeverLeaksIn()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);
        await _data.AddMembershipAsync("sales", CompanyB);

        // نقش قدرتمند فقط در شرکت B تعریف و (به اشتباه یا عمداً) فقط در B به او داده شده
        var powerful = await _data.AddRoleAsync(CompanyB, "همه‌کاره", true, InvoiceView, InvoiceCreate, UserView);
        await _data.AssignAsync("sales", CompanyB, powerful);

        Assert.NotEmpty(await PermissionsAsync("sales", CompanyB));
        Assert.Empty(await PermissionsAsync("sales", CompanyA));          // در شرکت A هیچ نقشی ندارد
    }

    [Fact]
    public async Task TheSamePerson_CanHaveDifferentRolesInDifferentCompanies()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);
        await _data.AddMembershipAsync("sales", CompanyB);

        var inA = await _data.AddRoleAsync(CompanyA, "منشی", true, InvoiceView);
        var inB = await _data.AddRoleAsync(CompanyB, "انباردار", true, WarehouseView);
        await _data.AssignAsync("sales", CompanyA, inA);
        await _data.AssignAsync("sales", CompanyB, inB);

        Assert.Equal(new[] { InvoiceView }, await PermissionsAsync("sales", CompanyA));
        Assert.Equal(new[] { WarehouseView }, await PermissionsAsync("sales", CompanyB));
    }

    [Fact]
    public async Task OwnershipOfOneCompany_DoesNotGiveOwnershipOfAnother()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("owner", CompanyA, owner: true);
        await _data.AddMembershipAsync("owner", CompanyB);          // در B فقط عضو ساده، بدون نقش

        Assert.NotEmpty(await PermissionsAsync("owner", CompanyA));
        Assert.Empty(await PermissionsAsync("owner", CompanyB));
    }

    [Fact]
    public async Task NonMembers_AndUnknownUsers_GetNothing()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("owner", CompanyA, owner: true);

        Assert.Empty(await PermissionsAsync("sales", CompanyA));            // عضو نیست
        Assert.Empty(await PermissionsAsync("ghost", CompanyA));            // کاربر ناموجود
        Assert.Empty(await PermissionsAsync("", CompanyA));
    }

    [Fact]
    public async Task RemovedMembership_CutsAccessImmediately()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);
        var role = await _data.AddRoleAsync(CompanyA, "فروش", true, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, role);

        Assert.NotEmpty(await PermissionsAsync("sales", CompanyA));

        await using (var context = _db.NewContext())
        {
            var membership = await context.UserTenants.SingleAsync(ut => ut.UserId == "sales");
            context.UserTenants.Remove(membership);        // حذف نرم
            await context.SaveChangesAsync();
        }

        Assert.Empty(await PermissionsAsync("sales", CompanyA));
    }

    [Fact]
    public async Task InactiveUser_InactiveTenant_InactiveOrDeletedRole_GiveNothing()
    {
        await ArrangeBaseAsync();
        await _data.AddUserAsync("blocked", active: false);
        await _data.AddMembershipAsync("blocked", CompanyA, owner: true);               // مالک هم باشد، غیرفعال است

        await _data.AddTenantAsync(3, active: false);
        await _data.AddMembershipAsync("owner", 3, owner: true);                        // شرکت غیرفعال

        await _data.AddMembershipAsync("sales", CompanyA);
        var inactiveRole = await _data.AddRoleAsync(CompanyA, "غیرفعال", false, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, inactiveRole);

        Assert.Empty(await PermissionsAsync("blocked", CompanyA));
        Assert.Empty(await PermissionsAsync("owner", 3));
        Assert.Empty(await PermissionsAsync("sales", CompanyA));                        // نقش غیرفعال

        // نقشِ حذف‌شده
        var role = await _data.AddRoleAsync(CompanyA, "موقت", true, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, role);
        Assert.Equal(new[] { InvoiceView }, await PermissionsAsync("sales", CompanyA));

        _db.Tenant.TenantId = CompanyA;
        await using (var context = _db.NewContext())
        {
            var entity = await context.TenantRoles.SingleAsync(r => r.Id == role);
            context.TenantRoles.Remove(entity);
            await context.SaveChangesAsync();
        }
        _db.Tenant.TenantId = null;

        Assert.Empty(await PermissionsAsync("sales", CompanyA));
    }

    [Fact]
    public async Task DeactivatedCatalogPermission_StopsWorkingEverywhere()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);
        var role = await _data.AddRoleAsync(CompanyA, "فروش", true, InvoiceView, InvoiceCreate);
        await _data.AssignAsync("sales", CompanyA, role);

        await using (var context = _db.NewContext())
        {
            var permission = await context.Permissions.SingleAsync(p => p.Name == InvoiceCreate);
            permission.IsActive = false;
            await context.SaveChangesAsync();
        }

        Assert.Equal(new[] { InvoiceView }, await PermissionsAsync("sales", CompanyA));
    }

    [Fact]
    public async Task PlatformPermissionStoredInATenantRole_IsIgnored()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);

        // حتی اگر به هر راهی (مثلاً دستکاری دیتابیس) نامِ سراسری در نقش شرکت بنشیند، اثری ندارد
        var role = await _data.AddRoleAsync(CompanyA, "هک", true, InvoiceView, PermissionView, MenuEdit);
        await _data.AssignAsync("sales", CompanyA, role);

        var permissions = await PermissionsAsync("sales", CompanyA);

        Assert.Equal(new[] { InvoiceView }, permissions);
    }

    [Fact]
    public async Task HasPermission_AndIsMember_Work()
    {
        await ArrangeBaseAsync();
        await _data.AddMembershipAsync("sales", CompanyA);
        var role = await _data.AddRoleAsync(CompanyA, "فروش", true, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, role);

        await using var context = _db.NewContext();
        var service = new TenantAccessService(context);

        Assert.True(await service.IsMemberAsync("sales", CompanyA));
        Assert.False(await service.IsMemberAsync("sales", CompanyB));
        Assert.True(await service.HasPermissionAsync("sales", CompanyA, InvoiceView));
        Assert.True(await service.HasPermissionAsync("sales", CompanyA, "invoice.view"));    // بدون حساسیت به حروف
        Assert.False(await service.HasPermissionAsync("sales", CompanyA, InvoiceCreate));
    }

    private async Task<string[]> PermissionsAsync(string userId, int tenantId)
    {
        await using var context = _db.NewContext();
        var set = await new TenantAccessService(context).GetPermissionsAsync(userId, tenantId);
        return set.OrderBy(x => x).ToArray();
    }
}