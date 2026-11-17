using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;
using static MyFirstCoreApp.Tests.TestSupport.SecurityTestHelper;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// افزودن فرد به شرکت (ساخت حساب + عضویت + مشخصات + نقش) با «UserManager واقعی» Identity.
/// همه‌چیز در یک تراکنش است: اگر یک مرحله رد شود، حساب هم ساخته نمی‌شود.
/// </summary>
public class TenantStaffServiceTests : IDisposable
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;
    private const string StrongPassword = "Abcd1234";

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;
    private readonly ServiceProvider _provider;

    public TenantStaffServiceTests()
    {
        _data = new SecurityTestHelper(_db);
        _db.Tenant.TenantId = CompanyA;
        _db.User.UserId = "owner";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _db.NewContext());
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    private async Task ArrangeAsync()
    {
        await _data.SeedPermissionsAsync();
        await _data.AddTenantAsync(CompanyA);
        await _data.AddTenantAsync(CompanyB);
        await _data.AddUserAsync("owner");
        await _data.AddMembershipAsync("owner", CompanyA, owner: true);
    }

    // ---------------------------------------------------------------
    // حساب جدید
    // ---------------------------------------------------------------

    [Fact]
    public async Task NewAccount_IsCreatedWithMembershipProfileAndRoles_AndCanLogInWithTheGivenPassword()
    {
        await ArrangeAsync();
        var role = await _data.AddRoleAsync(CompanyA, "منشی", true, InvoiceView);

        var result = await AddAsync(Request("sara@example.com", "سارا", "رضایی", "منشی", "۱۱۰", StrongPassword, role));

        Assert.True(result.CreatedNewAccount);

        await using var context = _db.NewContext();

        var user = await context.Users.SingleAsync(u => u.Id == result.UserId);
        Assert.Equal("sara@example.com", user.Email);
        Assert.Equal("sara@example.com", user.UserName);
        Assert.Equal("سارا رضایی", user.FullName);
        Assert.True(user.IsActive);

        var membership = await context.UserTenants.SingleAsync(ut => ut.UserId == result.UserId);
        Assert.Equal(CompanyA, membership.TenantId);
        Assert.False(membership.IsOwner);
        Assert.True(membership.IsDefault);                    // اولین شرکتِ این شخص
        Assert.Equal("سارا", membership.FirstName);
        Assert.Equal("رضایی", membership.LastName);
        Assert.Equal("منشی", membership.JobTitle);
        Assert.Equal("110", membership.InternalPhone);        // رقم فارسی نرمال شد

        // نقش و دسترسی
        var allowed = await new TenantAccessService(context).GetPermissionsAsync(result.UserId, CompanyA);
        Assert.Equal(new[] { InvoiceView }, allowed.ToArray());

        // رمز واقعاً ست شده است
        using var scope = _provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await manager.FindByIdAsync(result.UserId);
        Assert.True(await manager.CheckPasswordAsync(stored!, StrongPassword));
        Assert.False(await manager.CheckPasswordAsync(stored!, "Wrong1234"));
    }

    [Fact]
    public async Task NewAccount_WithoutRoles_IsAllowed_AndHasNoPermissions()
    {
        await ArrangeAsync();

        var result = await AddAsync(Request("nina@example.com", "نیلوفر", "کاظمی", null, null, StrongPassword));

        await using var context = _db.NewContext();
        Assert.Empty(await new TenantAccessService(context).GetPermissionsAsync(result.UserId, CompanyA));
    }

    // ---------------------------------------------------------------
    // کاربرِ موجود
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExistingUser_IsAddedToTheCompany_WithoutTouchingTheAccount()
    {
        await ArrangeAsync();
        await _data.AddUserAsync("worker", fullName: "نام اصلی حساب");
        await _data.AddMembershipAsync("worker", CompanyB, first: "نام در شرکت B", last: "فامیل B");

        // رمز و نام در درخواست «نادیده گرفته می‌شود»
        var role = await _data.AddRoleAsync(CompanyA, "فروش", true, InvoiceView);
        var result = await AddAsync(Request("worker@example.com", "نام دیگر", "فامیل دیگر", "کارشناس فروش", "201", "Hacked1234", role));

        Assert.False(result.CreatedNewAccount);
        Assert.Equal("worker", result.UserId);

        await using var context = _db.NewContext();

        var user = await context.Users.SingleAsync(u => u.Id == "worker");
        Assert.Equal("نام اصلی حساب", user.FullName);                  // حساب سراسری دست نخورد
        Assert.Null(user.PasswordHash);                                 // رمز عوض نشد

        // عضویت در A با مشخصات همین شرکت؛ پیش‌فرض نیست چون شرکت دیگری هم دارد
        var inA = await context.UserTenants.SingleAsync(ut => ut.UserId == "worker" && ut.TenantId == CompanyA);
        Assert.Equal("نام دیگر", inA.FirstName);
        Assert.Equal("201", inA.InternalPhone);
        Assert.False(inA.IsDefault);

        // مشخصات او در شرکت B سر جایش است
        var inB = await context.UserTenants.SingleAsync(ut => ut.UserId == "worker" && ut.TenantId == CompanyB);
        Assert.Equal("نام در شرکت B", inB.FirstName);
    }

    [Fact]
    public async Task ExistingUser_IsMatchedByEmailCaseInsensitively_AndCannotBeAddedTwice()
    {
        await ArrangeAsync();
        await _data.AddUserAsync("worker");

        await AddAsync(Request("WORKER@Example.com", "علی", "احمدی", null, null, null));

        var again = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("worker@example.com", "علی", "احمدی", null, null, null)));

        Assert.Contains("قبلاً عضو", again.Message);
    }

    // ---------------------------------------------------------------
    // اعتبارسنجی و اتمیک بودن
    // ---------------------------------------------------------------

    [Fact]
    public async Task Validation_Errors_AreClear()
    {
        await ArrangeAsync();

        var noPassword = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("a@example.com", "الف", "ب", null, null, null)));
        Assert.Contains("رمز عبور", noPassword.Message);

        var badEmail = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("not-an-email", "الف", "ب", null, null, StrongPassword)));
        Assert.Contains("ایمیل", badEmail.Message);

        var noEmail = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("  ", "الف", "ب", null, null, StrongPassword)));
        Assert.Contains("الزامی", noEmail.Message);

        // رمز ضعیف: خطای Identity به شکل پیام برمی‌گردد
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("weak@example.com", "الف", "ب", null, null, "123")));
    }

    [Fact]
    public async Task AnyFailure_LeavesNothingBehind_NoAccountNoMembership()
    {
        await ArrangeAsync();
        var role = await _data.AddRoleAsync(CompanyA, "منشی", true, InvoiceView);

        // اولی موفق؛ تلفن داخلی 110 گرفته می‌شود
        await AddAsync(Request("first@example.com", "الف", "ب", null, "110", StrongPassword, role));

        // دومی: تلفن داخلی تکراری ← رد؛ حساب جدید نباید ساخته شده باشد
        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("second@example.com", "ج", "د", null, "110", StrongPassword, role)));
        Assert.Contains("110", duplicate.Message);

        // سومی: نام خالی ← رد
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("third@example.com", "", "", null, null, StrongPassword)));

        // چهارمی: نقش نامعتبر ← رد
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("fourth@example.com", "ه", "و", null, null, StrongPassword, 999_999)));

        await using var context = _db.NewContext();
        foreach (var email in new[] { "second@example.com", "third@example.com", "fourth@example.com" })
            Assert.False(await context.Users.AnyAsync(u => u.Email == email), email);

        // فقط owner و first
        Assert.Equal(2, await context.UserTenants.CountAsync(ut => ut.TenantId == CompanyA));
    }

    [Fact]
    public async Task AnUnknownOrInactiveCompany_IsRejected()
    {
        await ArrangeAsync();
        await _data.AddTenantAsync(3, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("x@example.com", "الف", "ب", null, null, StrongPassword), tenantId: 999));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("y@example.com", "الف", "ب", null, null, StrongPassword), tenantId: 3));

        await using var context = _db.NewContext();
        Assert.False(await context.Users.AnyAsync(u => u.Email == "x@example.com" || u.Email == "y@example.com"));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static MemberCreateRequest Request(
        string email, string first, string last, string? title, string? extension, string? password, params int[] roleIds)
        => new()
        {
            Email = email,
            Password = password,
            FirstName = first,
            LastName = last,
            JobTitle = title,
            InternalPhone = extension,
            RoleIds = roleIds.ToList()
        };

    private async Task<MemberCreateResult> AddAsync(MemberCreateRequest request, int tenantId = CompanyA)
    {
        using var scope = _provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var service = new TenantStaffService(context, users, new TenantSecurityService(context));
        return await service.AddMemberAsync(tenantId, request);
    }
}
