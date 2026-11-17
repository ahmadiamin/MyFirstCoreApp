using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Persistence.Seed;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;
using static MyFirstCoreApp.Tests.TestSupport.SecurityTestHelper;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// منوی هر کاربر در شرکت فعالش: «کارشناس فروش» فقط منوی فروش، «انباردار» فقط انبار،
/// مالک همه‌ی منوهای شرکت (نه منوهای سیستمی)، مدیر سیستم همه.
/// </summary>
public class MenuTreeServiceTests : IDisposable
{
    private const int Company = 1;

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;

    public MenuTreeServiceTests()
    {
        _data = new SecurityTestHelper(_db);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>
    /// فروش (گروه)    : فاکتورها [Invoice.View]، ثبت فاکتور [Invoice.Create]
    /// انبار (گروه)    : کالاها [Warehouse.View]
    /// سیستم (گروه)   : منوها [Menu.Edit]، دسترسی‌ها [Permission.View]     ← سراسری
    /// شرکت‌های من     : [Tenancy.Create]                                  ← سطح حساب
    /// دفترچه‌ی تلفن   : بدون دسترسی
    /// منوی غیرفعال    : نباید بیاید
    /// </summary>
    private async Task ArrangeAsync()
    {
        await _data.SeedPermissionsAsync();
        await _data.AddTenantAsync(Company);

        await _data.AddUserAsync("owner");
        await _data.AddUserAsync("sales");
        await _data.AddUserAsync("storekeeper");
        await _data.AddUserAsync("nobody");               // عضو است ولی هیچ سمتی ندارد

        foreach (var user in new[] { "sales", "storekeeper", "nobody" })
            await _data.AddMembershipAsync(user, Company);
        await _data.AddMembershipAsync("owner", Company, owner: true);

        var salesRole = await _data.AddRoleAsync(Company, "کارشناس فروش", true, InvoiceView, InvoiceCreate);
        var storeRole = await _data.AddRoleAsync(Company, "انباردار", true, WarehouseView);
        await _data.AssignAsync("sales", Company, salesRole);
        await _data.AssignAsync("storekeeper", Company, storeRole);

        await using var context = _db.NewContext();

        var sales = await AddMenuAsync(context, "فروش", null, null, null, 10);
        await AddMenuAsync(context, "ثبت فاکتور", "/Invoicing/Invoices/Create", InvoiceCreate, sales.Id, 2);
        await AddMenuAsync(context, "فاکتورها", "/Invoicing/Invoices", InvoiceView, sales.Id, 1);

        var warehouse = await AddMenuAsync(context, "انبار", null, null, null, 20);
        await AddMenuAsync(context, "کالاها", "/Inventory/Products", WarehouseView, warehouse.Id, 1);

        var system = await AddMenuAsync(context, "سیستم", null, null, null, 30);
        await AddMenuAsync(context, "منوها", "/Admin/Menus", MenuEdit, system.Id, 1);
        await AddMenuAsync(context, "دسترسی‌ها", "/Admin/Permissions", PermissionView, system.Id, 2);

        await AddMenuAsync(context, "شرکت‌های من", "/Tenancy/Tenant", TenancyCreate, null, 40);
        await AddMenuAsync(context, "دفترچه‌ی تلفن", "/Company/Staff/Directory", null, null, 50);
        await AddMenuAsync(context, "قدیمی", "/Old", null, null, 60, active: false);
    }

    [Fact]
    public async Task SalesExpert_SeesOnlyTheSalesMenusAndTheOpenOnes()
    {
        await ArrangeAsync();

        var tree = await TreeAsync(Subject("sales"));

        Assert.Equal(new[] { "فروش", "شرکت‌های من", "دفترچه‌ی تلفن" }, tree.Select(n => n.Title).ToArray());

        // فرزندها به ترتیب SortOrder
        var salesGroup = tree.Single(n => n.Title == "فروش");
        Assert.Equal(new[] { "فاکتورها", "ثبت فاکتور" }, salesGroup.Children.Select(c => c.Title).ToArray());
    }

    [Fact]
    public async Task Storekeeper_SeesOnlyWarehouse_NotSales()
    {
        await ArrangeAsync();

        var tree = await TreeAsync(Subject("storekeeper"));

        Assert.Equal(new[] { "انبار", "شرکت‌های من", "دفترچه‌ی تلفن" }, tree.Select(n => n.Title).ToArray());
        Assert.Equal(new[] { "کالاها" }, tree.Single(n => n.Title == "انبار").Children.Select(c => c.Title).ToArray());
    }

    [Fact]
    public async Task Owner_SeesEveryCompanyMenu_ButNotPlatformMenus()
    {
        await ArrangeAsync();

        var tree = await TreeAsync(Subject("owner"));

        Assert.Equal(new[] { "فروش", "انبار", "شرکت‌های من", "دفترچه‌ی تلفن" }, tree.Select(n => n.Title).ToArray());
        Assert.DoesNotContain(tree, n => n.Title == "سیستم");           // منوهای مدیریت سیستم: فقط مدیر سیستم
    }

    [Fact]
    public async Task ASuperAdmin_SeesEverythingActive()
    {
        await ArrangeAsync();

        var tree = await TreeAsync(new PermissionSubject(true, true, "root", null));

        Assert.Equal(new[] { "فروش", "انبار", "سیستم", "شرکت‌های من", "دفترچه‌ی تلفن" }, tree.Select(n => n.Title).ToArray());
        Assert.Equal(2, tree.Single(n => n.Title == "سیستم").Children.Count);
        Assert.DoesNotContain(tree, n => n.Title == "قدیمی");           // غیرفعال
    }

    [Fact]
    public async Task AMemberWithoutAnyPosition_SeesOnlyTheOpenMenus()
    {
        await ArrangeAsync();

        var tree = await TreeAsync(Subject("nobody"));

        // گروه‌های خالی (بدون فرزند قابل‌نمایش) هم پنهان می‌شوند
        Assert.Equal(new[] { "شرکت‌های من", "دفترچه‌ی تلفن" }, tree.Select(n => n.Title).ToArray());
    }

    [Fact]
    public async Task WithoutAnActiveCompany_CompanyMenusDisappear_ButAccountLevelOnesStay()
    {
        await ArrangeAsync();

        // مالک است ولی هنوز شرکتی انتخاب نکرده (مثلاً در صفحه‌ی انتخاب شرکت)
        var tree = await TreeAsync(new PermissionSubject(true, false, "owner", null));

        Assert.Equal(new[] { "شرکت‌های من", "دفترچه‌ی تلفن" }, tree.Select(n => n.Title).ToArray());
    }

    [Fact]
    public async Task Anonymous_SeesNothing()
    {
        await ArrangeAsync();

        Assert.Empty(await TreeAsync(new PermissionSubject(false, false, null, Company)));
    }

    [Fact]
    public async Task APermissionRemovedFromTheCatalog_HidesItsMenuImmediately()
    {
        await ArrangeAsync();

        await using (var context = _db.NewContext())
        {
            var permission = await context.Permissions.SingleAsync(p => p.Name == InvoiceCreate);
            permission.IsActive = false;
            await context.SaveChangesAsync();
        }

        var tree = await TreeAsync(Subject("sales"));
        var salesGroup = tree.Single(n => n.Title == "فروش");

        Assert.Equal(new[] { "فاکتورها" }, salesGroup.Children.Select(c => c.Title).ToArray());
    }

    // ---------------------------------------------------------------
    // Seeder منوهای مدیریت شرکت
    // ---------------------------------------------------------------

    [Fact]
    public async Task CompanyMenuSeeder_CreatesItsOwnParent_AndIsIdempotent()
    {
        await using (var context = _db.NewContext())
        {
            await CompanyMenuSeeder.SeedAsync(context);
            await CompanyMenuSeeder.SeedAsync(context);       // بار دوم چیزی اضافه نمی‌کند
        }

        await using var verify = _db.NewContext();
        var menus = await verify.Menus.OrderBy(m => m.SortOrder).ToListAsync();

        Assert.Equal(4, menus.Count);                          // 1 گروه + 3 آیتم

        var parent = menus.Single(m => m.ParentId == null);
        Assert.Equal("مدیریت شرکت", parent.Title);

        var children = menus.Where(m => m.ParentId == parent.Id).OrderBy(m => m.SortOrder).ToList();
        Assert.Equal(new[] { "/Company/Staff", "/Company/Positions", "/Company/Staff/Directory" }, children.Select(c => c.Url).ToArray());
        Assert.Equal(new string?[] { "User.View", "Role.View", null }, children.Select(c => c.Permission).ToArray());
        Assert.All(children, c => Assert.True(c.IsActive));
    }

    [Fact]
    public async Task CompanyMenus_AreVisibleOnlyToThoseWhoMayUseThem()
    {
        await _data.SeedPermissionsAsync();
        await _data.AddTenantAsync(Company);
        await _data.AddUserAsync("owner");
        await _data.AddUserAsync("secretary");
        await _data.AddMembershipAsync("owner", Company, owner: true);
        await _data.AddMembershipAsync("secretary", Company);

        // «منشی»: فقط فاکتور؛ نه مدیریت کارکنان
        var role = await _data.AddRoleAsync(Company, "منشی", true, InvoiceView);
        await _data.AssignAsync("secretary", Company, role);

        await using (var context = _db.NewContext())
            await CompanyMenuSeeder.SeedAsync(context);

        var owner = await TreeAsync(Subject("owner"));
        var secretary = await TreeAsync(Subject("secretary"));

        // مالک: هر سه آیتم
        Assert.Equal(3, owner.Single(n => n.Title == "مدیریت شرکت").Children.Count);

        // منشی: فقط دفترچه‌ی تلفن
        var group = secretary.Single(n => n.Title == "مدیریت شرکت");
        Assert.Equal(new[] { "دفترچه‌ی تلفن داخلی" }, group.Children.Select(c => c.Title).ToArray());
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static PermissionSubject Subject(string userId) => new(true, false, userId, Company);

    private async Task<List<MenuNode>> TreeAsync(PermissionSubject subject)
    {
        await using var context = _db.NewContext();
        var evaluator = new PermissionEvaluator(new TenantAccessService(context), new PlatformPermissionChecker(context));
        return await new MenuTreeService(context, evaluator).GetVisibleTreeAsync(subject);
    }

    private static async Task<Menu> AddMenuAsync(
        ApplicationDbContext context, string title, string? url, string? permission, int? parentId, int sortOrder, bool active = true)
    {
        var menu = new Menu
        {
            Title = title,
            Url = url,
            Permission = permission,
            ParentId = parentId,
            SortOrder = sortOrder,
            IsActive = active
        };

        context.Menus.Add(menu);
        await context.SaveChangesAsync();
        return menu;
    }
}
