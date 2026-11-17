using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;
using static MyFirstCoreApp.Tests.TestSupport.SecurityTestHelper;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// مدیریت «سمت‌ها (نقش‌های شرکت)» و «افراد»: منشی، مدیر فروشگاه، کارشناس فروش،
/// با نام، فامیل، عنوان شغلی و تلفن داخلی.
/// </summary>
public class TenantSecurityServiceTests : IDisposable
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;

    public TenantSecurityServiceTests()
    {
        _data = new SecurityTestHelper(_db);
        _db.Tenant.TenantId = CompanyA;
        _db.User.UserId = "owner";
    }

    public void Dispose() => _db.Dispose();

    private async Task ArrangeAsync()
    {
        await _data.SeedPermissionsAsync();
        await _data.AddTenantAsync(CompanyA);
        await _data.AddTenantAsync(CompanyB);

        await _data.AddUserAsync("owner", fullName: "مدیر شرکت");
        await _data.AddUserAsync("secretary");
        await _data.AddUserAsync("sales", fullName: "علی احمدی");
        await _data.AddUserAsync("stranger");

        await _data.AddMembershipAsync("owner", CompanyA, owner: true, first: "رضا", last: "کریمی");
        await _data.AddMembershipAsync("secretary", CompanyA);
        await _data.AddMembershipAsync("sales", CompanyA);
        await _data.AddMembershipAsync("stranger", CompanyB);         // فقط عضو شرکت B
    }

    // ---------------------------------------------------------------
    // سمت‌ها
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateRole_StoresCanonicalNames_AndDropsAccountLevelPermissions()
    {
        await ArrangeAsync();

        var id = await CreateRoleAsync("کارشناس فروش", "invoice.view", InvoiceCreate, TenancyCreate);

        var role = await GetRoleAsync(id);
        Assert.Equal("کارشناس فروش", role!.Name);
        Assert.Equal(new[] { InvoiceCreate, InvoiceView }, role.PermissionNames);   // حروف یکدست؛ سطح حساب حذف
    }

    [Fact]
    public async Task CreateRole_RejectsPlatformUnknownAndRetiredPermissions()
    {
        await ArrangeAsync();

        // جلوی ارتقای دسترسی: نقش شرکت هرگز دسترسی سراسری نمی‌گیرد
        var platform = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRoleAsync("هک", PermissionView));
        Assert.Contains("مدیر سیستم", platform.Message);

        var menus = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRoleAsync("هک ۲", InvoiceView, MenuEdit));
        Assert.Contains("مدیر سیستم", menus.Message);

        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRoleAsync("ناشناخته", "Nothing.Here"));
        Assert.Contains("وجود ندارد", unknown.Message);

        var retired = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRoleAsync("قدیمی", Retired));
        Assert.Contains("غیرفعال", retired.Message);

        await using var verify = _db.NewContext();
        Assert.Empty(await verify.TenantRoles.ToListAsync());              // هیچ نقشِ نیمه‌کاره‌ای ساخته نشده
    }

    [Fact]
    public async Task CreateRole_ValidatesNameAndUniquenessPerCompany()
    {
        await ArrangeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRoleAsync("   ", InvoiceView));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRoleAsync(new string('ن', 101), InvoiceView));

        await CreateRoleAsync("منشی", InvoiceView);

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRoleAsync(" منشی ", WarehouseView));
        Assert.Contains("قبلاً", duplicate.Message);

        // همان نام در «شرکت دیگر» مشکلی ندارد
        _db.Tenant.TenantId = CompanyB;
        await using var context = _db.NewContext();
        var id = await new TenantSecurityService(context).CreateRoleAsync(CompanyB,
            new TenantRoleRequest { Name = "منشی", PermissionNames = { InvoiceView } });

        Assert.True(id > 0);
    }

    [Fact]
    public async Task UpdateRole_ReplacesPermissions_RenamesAndDeactivates()
    {
        await ArrangeAsync();
        var id = await CreateRoleAsync("منشی", InvoiceView, InvoiceCreate);

        await using (var context = _db.NewContext())
            await new TenantSecurityService(context).UpdateRoleAsync(CompanyA, id, new TenantRoleRequest
            {
                Name = "مدیر فروشگاه",
                Description = "مدیریت فروش و انبار",
                IsActive = false,
                PermissionNames = { InvoiceView, WarehouseView }
            });

        var role = await GetRoleAsync(id);
        Assert.Equal("مدیر فروشگاه", role!.Name);
        Assert.Equal("مدیریت فروش و انبار", role.Description);
        Assert.False(role.IsActive);
        Assert.Equal(new[] { InvoiceView, WarehouseView }, role.PermissionNames);
    }

    [Fact]
    public async Task UpdateRole_CanReAddAPermissionThatWasRemoved()
    {
        await ArrangeAsync();
        var id = await CreateRoleAsync("منشی", InvoiceView, InvoiceCreate);

        await UpdateRoleAsync(id, "منشی", InvoiceView);                       // برداشتن InvoiceCreate
        await UpdateRoleAsync(id, "منشی", InvoiceView, InvoiceCreate);        // دوباره دادن

        Assert.Equal(new[] { InvoiceCreate, InvoiceView }, (await GetRoleAsync(id))!.PermissionNames);
    }

    [Fact]
    public async Task UpdateRole_CannotRenameToAnExistingName_OrTouchAnotherCompanysRole()
    {
        await ArrangeAsync();
        var first = await CreateRoleAsync("منشی", InvoiceView);
        await CreateRoleAsync("انباردار", WarehouseView);

        var clash = await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateRoleAsync(first, "انباردار", InvoiceView));
        Assert.Contains("قبلاً", clash.Message);

        // یک نقشِ خودش را با همان نام نگه می‌دارد (تداخل با خودش نیست)
        await UpdateRoleAsync(first, "منشی", InvoiceView, InvoiceCreate);

        // شرکت B به نقش شرکت A دسترسی ندارد
        _db.Tenant.TenantId = CompanyB;
        await using var context = _db.NewContext();
        var service = new TenantSecurityService(context);

        var notFound = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRoleAsync(CompanyB, first, new TenantRoleRequest { Name = "هک", PermissionNames = { InvoiceView } }));
        Assert.Contains("پیدا نشد", notFound.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync(CompanyB, first));
        Assert.Null(await service.GetRoleAsync(CompanyB, first));
        Assert.Empty(await service.GetRolesAsync(CompanyB));
    }

    [Fact]
    public async Task DeleteRole_RemovesItFromEveryone_ButKeepsThePeople()
    {
        await ArrangeAsync();
        var roleId = await CreateRoleAsync("منشی", InvoiceView);
        await SetRolesAsync("secretary", roleId);

        await using (var context = _db.NewContext())
            await new TenantSecurityService(context).DeleteRoleAsync(CompanyA, roleId);

        await using var verify = _db.NewContext();
        var service = new TenantSecurityService(verify);

        Assert.Empty(await service.GetRolesAsync(CompanyA));

        var member = await service.GetMemberAsync(CompanyA, "secretary");
        Assert.NotNull(member);                                   // خودِ فرد سر جایش است
        Assert.Empty(member!.Roles);                              // ولی نقش از او برداشته شده

        Assert.Empty(await new TenantAccessService(verify).GetPermissionsAsync("secretary", CompanyA));
    }

    [Fact]
    public async Task GetRoles_ReportsPermissionAndMemberCounts()
    {
        await ArrangeAsync();
        var roleId = await CreateRoleAsync("کارشناس فروش", InvoiceView, InvoiceCreate);
        await SetRolesAsync("sales", roleId);
        await SetRolesAsync("secretary", roleId);

        await using var context = _db.NewContext();
        var summary = Assert.Single(await new TenantSecurityService(context).GetRolesAsync(CompanyA));

        Assert.Equal(2, summary.PermissionCount);
        Assert.Equal(2, summary.MemberCount);
    }

    [Fact]
    public async Task AssignablePermissions_AreGrouped_AndExcludePlatformAccountLevelAndRetired()
    {
        await ArrangeAsync();

        await using var context = _db.NewContext();
        var groups = await new TenantSecurityService(context).GetAssignablePermissionsAsync();

        var names = groups.SelectMany(g => g.Permissions).Select(p => p.Name).OrderBy(x => x).ToArray();

        Assert.Equal(new[] { InvoiceCreate, InvoiceView, RoleView, UserView, WarehouseView }, names);
        Assert.Contains(groups, g => g.GroupName == "فاکتور" && g.Permissions.Count == 2);
    }

    // ---------------------------------------------------------------
    // افراد و مشخصات پرسنلی
    // ---------------------------------------------------------------

    [Fact]
    public async Task Members_ShowNamesTitlesExtensionsAndRoles_OwnerFirst_OnlyThisCompany()
    {
        await ArrangeAsync();

        var secretaryRole = await CreateRoleAsync("منشی", InvoiceView);
        await UpdateProfileAsync("secretary", "مریم", "حسینی", "منشی", "101");
        await SetRolesAsync("secretary", secretaryRole);

        await using var context = _db.NewContext();
        var members = await new TenantSecurityService(context).GetMembersAsync(CompanyA);

        Assert.Equal(new[] { "owner", "secretary", "sales" }.OrderBy(x => x), members.Select(m => m.UserId).OrderBy(x => x));
        Assert.True(members[0].IsOwner);                                  // مالک اول
        Assert.DoesNotContain(members, m => m.UserId == "stranger");      // عضو شرکت دیگر دیده نمی‌شود

        var secretary = members.Single(m => m.UserId == "secretary");
        Assert.Equal("مریم حسینی", secretary.DisplayName);
        Assert.Equal("منشی", secretary.JobTitle);
        Assert.Equal("101", secretary.InternalPhone);
        Assert.Equal(new[] { "منشی" }, secretary.Roles.Select(r => r.RoleName));

        // بدون نام و فامیل شرکت ← نام حساب کاربری
        Assert.Equal("علی احمدی", members.Single(m => m.UserId == "sales").DisplayName);
        Assert.Equal("رضا کریمی", members.Single(m => m.UserId == "owner").DisplayName);
    }

    [Fact]
    public async Task UpdateProfile_NormalizesPersianDigits_AndTrimsText()
    {
        await ArrangeAsync();

        await UpdateProfileAsync("secretary", "  مریم ", " حسینی  ", "  منشی  ", "۱۰۲");

        var member = await GetMemberAsync("secretary");
        Assert.Equal("مریم", member!.FirstName);
        Assert.Equal("حسینی", member.LastName);
        Assert.Equal("منشی", member.JobTitle);
        Assert.Equal("102", member.InternalPhone);
    }

    [Fact]
    public async Task UpdateProfile_Validates()
    {
        await ArrangeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateProfileAsync("secretary", "", "حسینی", null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateProfileAsync("secretary", "مریم", " ", null, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateProfileAsync("secretary", "مریم", "حسینی", new string('x', 101), null));

        var letters = await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateProfileAsync("secretary", "مریم", "حسینی", null, "12a"));
        Assert.Contains("عدد", letters.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateProfileAsync("secretary", "مریم", "حسینی", null, "12345678901"));

        // خالی گذاشتن تلفن داخلی مجاز است
        await UpdateProfileAsync("secretary", "مریم", "حسینی", null, "   ");
        Assert.Null((await GetMemberAsync("secretary"))!.InternalPhone);
    }

    [Fact]
    public async Task InternalPhone_IsUniquePerCompany_ButCanBeKeptByItsOwner()
    {
        await ArrangeAsync();
        await UpdateProfileAsync("secretary", "مریم", "حسینی", "منشی", "101");

        var clash = await Assert.ThrowsAsync<InvalidOperationException>(
            () => UpdateProfileAsync("sales", "علی", "احمدی", "فروش", "101"));
        Assert.Contains("101", clash.Message);

        // خودِ دارنده می‌تواند دوباره ذخیره کند
        await UpdateProfileAsync("secretary", "مریم", "حسینی", "منشی ارشد", "101");
        Assert.Equal("منشی ارشد", (await GetMemberAsync("secretary"))!.JobTitle);

        // همان داخلی در شرکت دیگر مشکلی ندارد
        _db.Tenant.TenantId = CompanyB;
        await using var context = _db.NewContext();
        await new TenantSecurityService(context).UpdateMemberProfileAsync(CompanyB, "stranger",
            new MemberProfileRequest { FirstName = "سارا", LastName = "رضایی", InternalPhone = "101" });
    }

    [Fact]
    public async Task AMemberOfAnotherCompany_CannotBeSeenOrChanged()
    {
        await ArrangeAsync();
        var roleId = await CreateRoleAsync("منشی", InvoiceView);

        // «stranger» عضو شرکت B است؛ از شرکت A هیچ کاری روی او ممکن نیست
        var update = await Assert.ThrowsAsync<InvalidOperationException>(
            () => UpdateProfileAsync("stranger", "هک", "هکر", null, null));
        Assert.Contains("عضو شرکت نیست", update.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SetRolesAsync("stranger", roleId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => RemoveAsync("stranger"));
        Assert.Null(await GetMemberAsync("stranger"));
    }

    // ---------------------------------------------------------------
    // نقش‌های افراد
    // ---------------------------------------------------------------

    [Fact]
    public async Task SetMemberRoles_ReplacesTheAssignments()
    {
        await ArrangeAsync();
        var sales = await CreateRoleAsync("کارشناس فروش", InvoiceView);
        var store = await CreateRoleAsync("انباردار", WarehouseView);

        await SetRolesAsync("secretary", sales, store);
        Assert.Equal(new[] { "انباردار", "کارشناس فروش" }, (await GetMemberAsync("secretary"))!.Roles.Select(r => r.RoleName).OrderBy(x => x));

        await SetRolesAsync("secretary", store);
        Assert.Equal(new[] { "انباردار" }, (await GetMemberAsync("secretary"))!.Roles.Select(r => r.RoleName));

        await SetRolesAsync("secretary");                                 // بدون هیچ نقش
        Assert.Empty((await GetMemberAsync("secretary"))!.Roles);
    }

    [Fact]
    public async Task SetMemberRoles_RejectsOwner_InactiveAndForeignRoles()
    {
        await ArrangeAsync();
        var active = await CreateRoleAsync("فعال", InvoiceView);

        await using (var context = _db.NewContext())
        {
            var service = new TenantSecurityService(context);
            var inactive = await service.CreateRoleAsync(CompanyA,
                new TenantRoleRequest { Name = "غیرفعال", IsActive = false, PermissionNames = { InvoiceView } });

            var owner = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetMemberRolesAsync(CompanyA, "owner", new[] { active }));
            Assert.Contains("مالک", owner.Message);

            var disabled = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetMemberRolesAsync(CompanyA, "secretary", new[] { inactive }));
            Assert.Contains("غیرفعال", disabled.Message);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetMemberRolesAsync(CompanyA, "secretary", new[] { 999_999 }));
        }

        // نقشِ «شرکت B» را نمی‌شود به عضو شرکت A داد
        _db.Tenant.TenantId = CompanyB;
        var foreignRole = await _data.AddRoleAsync(CompanyB, "نقش B", true, InvoiceView);
        _db.Tenant.TenantId = CompanyA;

        await Assert.ThrowsAsync<InvalidOperationException>(() => SetRolesAsync("secretary", foreignRole));

        Assert.Empty((await GetMemberAsync("secretary"))!.Roles);
    }

    [Fact]
    public async Task RemoveMember_RemovesMembershipAndRoles_ButNotTheAccount_AndNeverTheOwner()
    {
        await ArrangeAsync();
        var roleId = await CreateRoleAsync("منشی", InvoiceView);
        await SetRolesAsync("secretary", roleId);

        await RemoveAsync("secretary");

        await using var verify = _db.NewContext();
        Assert.Null(await new TenantSecurityService(verify).GetMemberAsync(CompanyA, "secretary"));
        Assert.True(await verify.Users.AnyAsync(u => u.Id == "secretary"));                 // حساب سر جایش است
        Assert.Empty(await new TenantAccessService(verify).GetPermissionsAsync("secretary", CompanyA));

        var owner = await Assert.ThrowsAsync<InvalidOperationException>(() => RemoveAsync("owner"));
        Assert.Contains("مالک", owner.Message);
    }

    // ---------------------------------------------------------------
    // سناریوی کامل: «کارشناس فروش فقط فاکتور ثبت کند»
    // ---------------------------------------------------------------

    [Fact]
    public async Task EndToEnd_ASalesExpertDefinedByTheCompany_CanOnlyDoWhatHisPositionAllows()
    {
        await ArrangeAsync();

        // مالک سمت «کارشناس فروش» را با دسترسی ثبت و دیدن فاکتور تعریف می‌کند
        var position = await CreateRoleAsync("کارشناس فروش", InvoiceCreate, InvoiceView);

        // و «علی احمدی» با تلفن داخلی ۲۰۱ را در آن سمت می‌گذارد
        await UpdateProfileAsync("sales", "علی", "احمدی", "کارشناس فروش", "201");
        await SetRolesAsync("sales", position);

        await using var context = _db.NewContext();
        var evaluator = new PermissionEvaluator(new TenantAccessService(context), new PlatformPermissionChecker(context));
        var sales = new PermissionSubject(true, false, "sales", CompanyA);

        Assert.True(await evaluator.HasAsync(sales, InvoiceCreate));
        Assert.True(await evaluator.HasAsync(sales, InvoiceView));
        Assert.False(await evaluator.HasAsync(sales, WarehouseView));         // انبار نه
        Assert.False(await evaluator.HasAsync(sales, UserView));              // مدیریت کاربران نه
        Assert.False(await evaluator.HasAsync(sales, PermissionView));        // مدیریت سیستم نه

        // مالک سمت را عوض می‌کند ← اثر فوری
        await using (var edit = _db.NewContext())
            await new TenantSecurityService(edit).UpdateRoleAsync(CompanyA, position, new TenantRoleRequest
            {
                Name = "کارشناس فروش",
                PermissionNames = { InvoiceView }
            });

        await using var after = _db.NewContext();
        var evaluatorAfter = new PermissionEvaluator(new TenantAccessService(after), new PlatformPermissionChecker(after));

        Assert.False(await evaluatorAfter.HasAsync(sales, InvoiceCreate));
        Assert.True(await evaluatorAfter.HasAsync(sales, InvoiceView));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<int> CreateRoleAsync(string name, params string[] permissions)
    {
        await using var context = _db.NewContext();
        return await new TenantSecurityService(context).CreateRoleAsync(CompanyA,
            new TenantRoleRequest { Name = name, PermissionNames = permissions.ToList() });
    }

    private async Task UpdateRoleAsync(int id, string name, params string[] permissions)
    {
        await using var context = _db.NewContext();
        await new TenantSecurityService(context).UpdateRoleAsync(CompanyA, id,
            new TenantRoleRequest { Name = name, PermissionNames = permissions.ToList() });
    }

    private async Task<TenantRoleDetails?> GetRoleAsync(int id)
    {
        await using var context = _db.NewContext();
        return await new TenantSecurityService(context).GetRoleAsync(CompanyA, id);
    }

    private async Task<TenantMemberInfo?> GetMemberAsync(string userId)
    {
        await using var context = _db.NewContext();
        return await new TenantSecurityService(context).GetMemberAsync(CompanyA, userId);
    }

    private async Task UpdateProfileAsync(string userId, string first, string last, string? title, string? extension)
    {
        await using var context = _db.NewContext();
        await new TenantSecurityService(context).UpdateMemberProfileAsync(CompanyA, userId, new MemberProfileRequest
        {
            FirstName = first,
            LastName = last,
            JobTitle = title,
            InternalPhone = extension
        });
    }

    private async Task SetRolesAsync(string userId, params int[] roleIds)
    {
        await using var context = _db.NewContext();
        await new TenantSecurityService(context).SetMemberRolesAsync(CompanyA, userId, roleIds);
    }

    private async Task RemoveAsync(string userId)
    {
        await using var context = _db.NewContext();
        await new TenantSecurityService(context).RemoveMemberAsync(CompanyA, userId);
    }
}