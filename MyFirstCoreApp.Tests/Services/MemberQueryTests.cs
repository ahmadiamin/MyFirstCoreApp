using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;
using static MyFirstCoreApp.Tests.TestSupport.SecurityTestHelper;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// تست پسرفت: «GetMemberAsync» با فیلتر روی خروجیِ ساخته‌شده با سازنده، قابل ترجمه به SQL نبود
/// (خطای «LINQ expression could not be translated» هنگام «ارسال کد تعیین رمز»).
/// </summary>
public class MemberQueryTests : IDisposable
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;

    public MemberQueryTests()
    {
        _data = new SecurityTestHelper(_db);
        _db.Tenant.TenantId = CompanyA;
    }

    public void Dispose() => _db.Dispose();

    private async Task ArrangeAsync()
    {
        await _data.SeedPermissionsAsync();
        await _data.AddTenantAsync(CompanyA);
        await _data.AddTenantAsync(CompanyB);

        await _data.AddUserAsync("owner", fullName: "مدیر");
        await _data.AddUserAsync("sales", fullName: "علی احمدی");
        await _data.AddUserAsync("stranger");

        await _data.AddMembershipAsync("owner", CompanyA, owner: true, first: "رضا", last: "کریمی");
        await _data.AddMembershipAsync("sales", CompanyA, first: "علی", last: "احمدی");
        await _data.AddMembershipAsync("stranger", CompanyB);
    }

    [Fact]
    public async Task GetMember_ReturnsExactlyThatMember_WithItsRoles()
    {
        await ArrangeAsync();
        var roleId = await _data.AddRoleAsync(CompanyA, "کارشناس فروش", true, InvoiceView);
        await _data.AssignAsync("sales", CompanyA, roleId);

        await using var context = _db.NewContext();
        var member = await new TenantSecurityService(context).GetMemberAsync(CompanyA, "sales");

        Assert.NotNull(member);
        Assert.Equal("sales", member!.UserId);
        Assert.Equal("علی احمدی", member.DisplayName);
        Assert.False(member.IsOwner);
        Assert.Equal(new[] { "کارشناس فروش" }, member.Roles.Select(r => r.RoleName));
    }

    [Fact]
    public async Task GetMember_DoesNotReturnOtherMembers_OrMembersOfAnotherCompany_OrUnknownIds()
    {
        await ArrangeAsync();

        await using var context = _db.NewContext();
        var service = new TenantSecurityService(context);

        Assert.True((await service.GetMemberAsync(CompanyA, "owner"))!.IsOwner);
        Assert.Null(await service.GetMemberAsync(CompanyA, "stranger"));      // عضو شرکت B
        Assert.Null(await service.GetMemberAsync(CompanyA, "nobody"));
        Assert.Null(await service.GetMemberAsync(CompanyA, ""));
    }

    [Fact]
    public async Task GetMembers_AndGetMember_AgreeOnEveryMember()
    {
        await ArrangeAsync();

        await using var context = _db.NewContext();
        var service = new TenantSecurityService(context);

        var all = await service.GetMembersAsync(CompanyA);
        Assert.Equal(2, all.Count);

        foreach (var expected in all)
        {
            var single = await service.GetMemberAsync(CompanyA, expected.UserId);

            Assert.NotNull(single);
            Assert.Equal(expected.DisplayName, single!.DisplayName);
            Assert.Equal(expected.IsOwner, single.IsOwner);
            Assert.Equal(expected.FullName, single.FullName);
        }
    }
}
