using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// «با چه سمتی وارد می‌شوی؟»: کسی که هم مدیر یک کسب‌وکار است و هم کارمند جای دیگر، اول انتخاب می‌کند؛
/// با یک سمت یا بدون سمت، سؤالی نیست. و فهرست شرکت‌ها (TenantService) بر اساس سمتِ انتخاب‌شده فیلتر می‌شود.
/// </summary>
public class LoginCapacityTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;

    public LoginCapacityTests()
    {
        _data = new SecurityTestHelper(_db);
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------
    // LoginCapacityService
    // ---------------------------------------------------------------

    [Fact]
    public async Task OnlyAManager_IsNeverAsked()
    {
        await _data.AddUserAsync("manager");
        await _data.AddTenantAsync(10);
        await _data.AddTenantAsync(11);
        await _data.AddMembershipAsync("manager", 10, owner: true);
        await _data.AddMembershipAsync("manager", 11, owner: true);

        var info = await GetAsync("manager");

        Assert.False(info.NeedsChoice);
        Assert.Equal(LoginCapacity.Owner, info.OnlyCapacity);
        Assert.Equal(new[] { "شرکت 10", "شرکت 11" }, info.OwnerTenants.Select(t => t.Name));
        Assert.Empty(info.EmployeeTenants);
    }

    [Fact]
    public async Task OnlyAnEmployee_IsNeverAsked()
    {
        await _data.AddUserAsync("worker");
        await _data.AddTenantAsync(10);
        await _data.AddMembershipAsync("worker", 10);

        var info = await GetAsync("worker");

        Assert.False(info.NeedsChoice);
        Assert.Equal(LoginCapacity.Employee, info.OnlyCapacity);
    }

    [Fact]
    public async Task ManagerAndEmployee_MustChoose_AndSeesTheRightCompaniesUnderEach()
    {
        await _data.AddUserAsync("both");
        await _data.AddTenantAsync(10);       // مدیر
        await _data.AddTenantAsync(11);       // کارمند
        await _data.AddTenantAsync(12);       // کارمند
        await _data.AddMembershipAsync("both", 10, owner: true);
        await _data.AddMembershipAsync("both", 11);
        await _data.AddMembershipAsync("both", 12);

        var info = await GetAsync("both");

        Assert.True(info.NeedsChoice);
        Assert.Null(info.OnlyCapacity);
        Assert.Equal(new[] { 10 }, info.TenantsFor(LoginCapacity.Owner).Select(t => t.Id));
        Assert.Equal(new[] { 11, 12 }, info.TenantsFor(LoginCapacity.Employee).Select(t => t.Id));
    }

    [Fact]
    public async Task InactiveDeletedTenants_AndRemovedMemberships_DoNotCount()
    {
        await _data.AddUserAsync("u");
        await _data.AddTenantAsync(10);
        await _data.AddTenantAsync(11, active: false);     // غیرفعال
        await _data.AddTenantAsync(12);
        await _data.AddTenantAsync(13);
        await _data.AddMembershipAsync("u", 10, owner: true);
        await _data.AddMembershipAsync("u", 11);           // شرکت غیرفعال ← سمت کارمند حساب نمی‌شود
        await _data.AddMembershipAsync("u", 12);
        await _data.AddMembershipAsync("u", 13);

        await using (var context = _db.NewContext())
        {
            // شرکت 12 حذف نرم، و عضویت در 13 برداشته شده
            context.Tenants.Remove(await context.Tenants.SingleAsync(t => t.Id == 12));
            context.UserTenants.Remove(await context.UserTenants.SingleAsync(ut => ut.UserId == "u" && ut.TenantId == 13));
            await context.SaveChangesAsync();
        }

        var info = await GetAsync("u");

        Assert.Equal(new[] { 10 }, info.OwnerTenants.Select(t => t.Id));
        Assert.Empty(info.EmployeeTenants);
        Assert.False(info.NeedsChoice);                      // پس دیگر نمی‌پرسد
    }

    [Fact]
    public async Task AUserWithoutMembership_AndUnknownUsers_HaveNothing()
    {
        await _data.AddUserAsync("loner");

        Assert.Null((await GetAsync("loner")).OnlyCapacity);
        Assert.False((await GetAsync("ghost")).NeedsChoice);
        Assert.False((await GetAsync("")).NeedsChoice);
    }

    [Fact]
    public async Task OtherUsersMemberships_NeverLeakIn()
    {
        await _data.AddUserAsync("a");
        await _data.AddUserAsync("b");
        await _data.AddTenantAsync(10);
        await _data.AddMembershipAsync("a", 10, owner: true);
        await _data.AddMembershipAsync("b", 10);

        Assert.Equal(LoginCapacity.Owner, (await GetAsync("a")).OnlyCapacity);
        Assert.Equal(LoginCapacity.Employee, (await GetAsync("b")).OnlyCapacity);
    }

    // ---------------------------------------------------------------
    // TenantService: فهرست شرکت‌ها بر اساس سمتِ انتخاب‌شده
    // ---------------------------------------------------------------

    [Fact]
    public async Task TenantList_IsFilteredByTheChosenCapacity()
    {
        await ArrangeBothAsync();
        var accessor = new FakeCapacityAccessor();

        // انتخابی نشده ← همه
        Assert.Equal(new[] { 10, 11, 12 }, await TenantIdsAsync("both", accessor));

        accessor.Set("both", LoginCapacity.Owner);
        Assert.Equal(new[] { 10 }, await TenantIdsAsync("both", accessor));

        accessor.Set("both", LoginCapacity.Employee);
        Assert.Equal(new[] { 11, 12 }, await TenantIdsAsync("both", accessor));
    }

    [Fact]
    public async Task TenantRelations_AreFilteredTheSameWay()
    {
        await ArrangeBothAsync();
        var accessor = new FakeCapacityAccessor();
        accessor.Set("both", LoginCapacity.Employee);

        await using var context = _db.NewContext();
        var relations = await new TenantService(context, accessor).GetUserTenantRelationsAsync("both");

        Assert.Equal(new[] { 11, 12 }, relations.Select(r => r.TenantId).OrderBy(x => x));
        Assert.All(relations, r => Assert.False(r.IsOwner));
    }

    [Fact]
    public async Task AChoiceMadeByAnotherUser_DoesNotAffectThisOne()
    {
        await ArrangeBothAsync();
        var accessor = new FakeCapacityAccessor();
        accessor.Set("someone-else", LoginCapacity.Owner);

        Assert.Equal(new[] { 10, 11, 12 }, await TenantIdsAsync("both", accessor));
    }

    [Fact]
    public async Task WithoutAnAccessor_TheServiceBehavesAsBefore()
    {
        await ArrangeBothAsync();

        await using var context = _db.NewContext();
        var tenants = await new TenantService(context).GetUserTenantsAsync("both");

        Assert.Equal(new[] { 10, 11, 12 }, tenants.Select(t => t.Id).OrderBy(x => x));
    }

    [Fact]
    public async Task AnEmployeeWhoCreatesACompany_StillSeesTheNewOne()
    {
        await _data.AddUserAsync("worker");
        await _data.AddTenantAsync(10);
        await _data.AddMembershipAsync("worker", 10);

        var accessor = new FakeCapacityAccessor();
        accessor.Set("worker", LoginCapacity.Employee);

        await using (var context = _db.NewContext())
            await new TenantService(context, accessor).CreateAsync(new Tenant { Name = "شرکت تازه" }, "worker");

        // سمتِ انتخاب‌شده به «مدیر» عوض شد، وگرنه شرکتِ تازه‌ساخته در لیست نمی‌آمد
        Assert.Equal(LoginCapacity.Owner, accessor.GetChosen("worker"));

        await using var verify = _db.NewContext();
        var names = (await new TenantService(verify, accessor).GetUserTenantsAsync("worker")).Select(t => t.Name).ToArray();
        Assert.Equal(new[] { "شرکت تازه" }, names);
    }

    [Fact]
    public async Task CreatingACompany_DoesNotTouchAnOwnerChoiceOrNoChoice()
    {
        await _data.AddUserAsync("boss");

        var none = new FakeCapacityAccessor();
        await using (var context = _db.NewContext())
            await new TenantService(context, none).CreateAsync(new Tenant { Name = "الف" }, "boss");
        Assert.Null(none.GetChosen("boss"));

        var owner = new FakeCapacityAccessor();
        owner.Set("boss", LoginCapacity.Owner);
        await using (var context = _db.NewContext())
            await new TenantService(context, owner).CreateAsync(new Tenant { Name = "ب" }, "boss");
        Assert.Equal(LoginCapacity.Owner, owner.GetChosen("boss"));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task ArrangeBothAsync()
    {
        await _data.AddUserAsync("both");
        await _data.AddTenantAsync(10);
        await _data.AddTenantAsync(11);
        await _data.AddTenantAsync(12);
        await _data.AddMembershipAsync("both", 10, owner: true);
        await _data.AddMembershipAsync("both", 11);
        await _data.AddMembershipAsync("both", 12);
    }

    private async Task<LoginCapacityInfo> GetAsync(string userId)
    {
        await using var context = _db.NewContext();
        return await new LoginCapacityService(context).GetAsync(userId);
    }

    private async Task<int[]> TenantIdsAsync(string userId, ILoginCapacityAccessor accessor)
    {
        await using var context = _db.NewContext();
        var tenants = await new TenantService(context, accessor).GetUserTenantsAsync(userId);
        return tenants.Select(t => t.Id).OrderBy(x => x).ToArray();
    }

    /// <summary>جایگزین سشن: انتخاب به «کاربر» بسته است (مثل پیاده‌سازی واقعی).</summary>
    private sealed class FakeCapacityAccessor : ILoginCapacityAccessor
    {
        private readonly Dictionary<string, LoginCapacity> _chosen = new();

        public LoginCapacity? GetChosen(string userId)
            => _chosen.TryGetValue(userId, out var capacity) ? capacity : null;

        public void Set(string userId, LoginCapacity capacity) => _chosen[userId] = capacity;

        public void Clear() => _chosen.Clear();
    }
}
