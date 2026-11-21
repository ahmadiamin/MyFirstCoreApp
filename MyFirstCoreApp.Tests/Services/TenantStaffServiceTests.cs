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
/// افزودن کارمند با «موبایل» و تعیین رمز با پیامک: مالک رمز نمی‌دهد و نمی‌داند؛ حساب جدید با رمز تصادفی ساخته
/// می‌شود و کد تعیین رمز به موبایل کارمند می‌رود. حسابِ موجود (مثلاً مدیر شرکت دیگر) بدون تغییر اضافه می‌شود.
/// (UserManager واقعی Identity؛ پیامک جعلی.)
/// </summary>
public class TenantStaffServiceTests : IDisposable
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;
    private const string Mobile = "09123456789";

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;
    private readonly ServiceProvider _provider;
    private readonly FakeOtpService _otp = new();

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
                options.User.RequireUniqueEmail = false;
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
    public async Task NewAccount_UsesTheMobileAsLogin_HasNoEmail_AndNobodyKnowsItsPassword()
    {
        await ArrangeAsync();
        var role = await _data.AddRoleAsync(CompanyA, "منشی", true, InvoiceView);

        // ارقام فارسی نرمال می‌شوند
        var result = await AddAsync(Request("۰۹۱۲۳۴۵۶۷۸۹", "سارا", "رضایی", "منشی", "۱۱۰", role));

        Assert.True(result.CreatedNewAccount);
        Assert.True(result.PasswordSmsAccepted);
        Assert.Equal("0912***6789", result.MaskedMobile);

        await using var context = _db.NewContext();

        var user = await context.Users.SingleAsync(u => u.Id == result.UserId);
        Assert.Equal(Mobile, user.UserName);                // ورود با موبایل
        Assert.Equal(Mobile, user.PhoneNumber);
        Assert.True(user.PhoneNumberConfirmed);
        Assert.True(user.EmailConfirmed);                   // سیاست «حساب تأییدشده»
        Assert.Null(user.Email);                            // ایمیلِ تأییدنشده ذخیره نمی‌شود
        Assert.True(user.IsActive);
        Assert.Equal("سارا رضایی", user.FullName);
        Assert.False(string.IsNullOrEmpty(user.PasswordHash));

        // مشخصات و سمت در همین شرکت
        var membership = await context.UserTenants.SingleAsync(ut => ut.UserId == result.UserId);
        Assert.False(membership.IsOwner);
        Assert.True(membership.IsDefault);
        Assert.Equal("سارا", membership.FirstName);
        Assert.Equal("منشی", membership.JobTitle);
        Assert.Equal("110", membership.InternalPhone);

        var permissions = await new TenantAccessService(context).GetPermissionsAsync(result.UserId, CompanyA);
        Assert.Equal(new[] { InvoiceView }, permissions.ToArray());

        // رمزِ تصادفی است: هیچ رمزِ رایجی جواب نمی‌دهد
        using var scope = _provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await manager.FindByIdAsync(result.UserId))!;
        foreach (var guess in new[] { "Abcd1234", "12345678", Mobile, "Password1!" })
            Assert.False(await manager.CheckPasswordAsync(stored, guess), guess);

        // کدِ تعیین رمز به همان موبایل رفت
        Assert.Equal(new[] { Mobile }, _otp.RequestedMobiles);
    }

    [Fact]
    public async Task ASmsFailure_NeverUndoesTheAccount_ButTheOwnerIsToldItDidNotGo()
    {
        await ArrangeAsync();

        _otp.Accept = false;
        var rejected = await AddAsync(Request(Mobile, "الف", "ب", null, null));
        Assert.True(rejected.CreatedNewAccount);
        Assert.False(rejected.PasswordSmsAccepted);

        _otp.ThrowOnRequest = true;
        var crashed = await AddAsync(Request("09120000002", "ج", "د", null, null));
        Assert.True(crashed.CreatedNewAccount);
        Assert.False(crashed.PasswordSmsAccepted);

        await using var context = _db.NewContext();
        Assert.Equal(2, await context.UserTenants.CountAsync(ut => ut.TenantId == CompanyA && !ut.IsOwner));
    }

    // ---------------------------------------------------------------
    // حسابِ موجود (مدیر شرکتی دیگر یا کارمند شرکتی دیگر)
    // ---------------------------------------------------------------

    [Fact]
    public async Task ABusinessOwnerElsewhere_CanBeAddedAsAnEmployee_WithTheSameAccount()
    {
        await ArrangeAsync();

        // «مدیر» مالک شرکت B است و موبایلش تأیید شده
        await AddUserWithPhoneAsync("boss", Mobile, confirmed: true, fullName: "نام اصلی حساب");
        await _data.AddMembershipAsync("boss", CompanyB, owner: true);

        var role = await _data.AddRoleAsync(CompanyA, "فروش", true, InvoiceView);
        var result = await AddAsync(Request(Mobile, "نام در شرکت A", "فامیل A", "کارشناس فروش", "201", role));

        Assert.False(result.CreatedNewAccount);
        Assert.Equal("boss", result.UserId);
        Assert.Empty(_otp.RequestedMobiles);                    // رمز و پیامکی در کار نیست

        await using var context = _db.NewContext();

        var user = await context.Users.SingleAsync(u => u.Id == "boss");
        Assert.Equal("نام اصلی حساب", user.FullName);           // حساب سراسری دست نخورد
        Assert.Null(user.PasswordHash);

        // دو عضویتِ همزمان با دو سمت: مالک در B، کارمند در A (همان که صفحه‌ی «با چه سمتی؟» را می‌خواهد)
        var memberships = await context.UserTenants.Where(ut => ut.UserId == "boss").ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.True(memberships.Single(m => m.TenantId == CompanyB).IsOwner);

        var inA = memberships.Single(m => m.TenantId == CompanyA);
        Assert.False(inA.IsOwner);
        Assert.False(inA.IsDefault);                            // شرکت پیش‌فرضش همان B است
        Assert.Equal("نام در شرکت A", inA.FirstName);

        // دسترسی در A فقط از سمتِ کارمندی؛ در B مالک است
        var access = new TenantAccessService(context);
        Assert.Equal(new[] { InvoiceView }, (await access.GetPermissionsAsync("boss", CompanyA)).ToArray());
        Assert.NotEmpty(await access.GetPermissionsAsync("boss", CompanyB));
    }

    [Fact]
    public async Task AnUnconfirmedAccountWithThatNumber_IsRejected_SoNobodyCanClaimSomeoneElsesNumber()
    {
        await ArrangeAsync();
        await AddUserWithPhoneAsync("claimed", Mobile, confirmed: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request(Mobile, "الف", "ب", null, null)));

        Assert.Contains("تأیید نشده", ex.Message);

        await using var context = _db.NewContext();
        Assert.Empty(await context.UserTenants.Where(ut => ut.UserId == "claimed").ToListAsync());
        Assert.Empty(_otp.RequestedMobiles);
    }

    [Fact]
    public async Task InactiveOrAlreadyMember_AreRejected()
    {
        await ArrangeAsync();

        await AddUserWithPhoneAsync("blocked", "09120000003", confirmed: true, active: false);
        var inactive = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("09120000003", "الف", "ب", null, null)));
        Assert.Contains("غیرفعال", inactive.Message);

        await AddUserWithPhoneAsync("member", "09120000004", confirmed: true);
        await _data.AddMembershipAsync("member", CompanyA);
        var already = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("09120000004", "الف", "ب", null, null)));
        Assert.Contains("قبلاً عضو", already.Message);
    }

    // ---------------------------------------------------------------
    // اعتبارسنجی و اتمیک بودن
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9123456789")]        // بدون صفر
    [InlineData("0912345678")]        // ۱۰ رقم
    [InlineData("091234567890")]      // ۱۲ رقم
    [InlineData("08123456789")]       // شروع نادرست
    [InlineData("0912345678a")]
    public async Task InvalidMobiles_AreRejected(string mobile)
    {
        await ArrangeAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request(mobile, "الف", "ب", null, null)));

        Assert.Contains("موبایل", ex.Message);
    }

    [Fact]
    public async Task AnyFailure_LeavesNothingBehind_AndSendsNoSms()
    {
        await ArrangeAsync();
        var role = await _data.AddRoleAsync(CompanyA, "منشی", true, InvoiceView);

        await AddAsync(Request("09120000010", "الف", "ب", null, "110", role));
        _otp.RequestedMobiles.Clear();

        // تلفن داخلی تکراری
        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("09120000011", "ج", "د", null, "110", role)));
        Assert.Contains("110", duplicate.Message);

        // نام خالی
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("09120000012", "", "", null, null)));

        // نقش نامعتبر
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request("09120000013", "ه", "و", null, null, 999_999)));

        await using var context = _db.NewContext();
        foreach (var number in new[] { "09120000011", "09120000012", "09120000013" })
            Assert.False(await context.Users.AnyAsync(u => u.PhoneNumber == number), number);

        Assert.Empty(_otp.RequestedMobiles);                    // برای شکست‌خورده‌ها پیامکی نرفت
    }

    [Fact]
    public async Task AnUnknownOrInactiveCompany_IsRejected()
    {
        await ArrangeAsync();
        await _data.AddTenantAsync(3, active: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request(Mobile, "الف", "ب", null, null), tenantId: 999));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AddAsync(Request(Mobile, "الف", "ب", null, null), tenantId: 3));

        await using var context = _db.NewContext();
        Assert.False(await context.Users.AnyAsync(u => u.PhoneNumber == Mobile));
    }

    // ---------------------------------------------------------------
    // ارسال دوباره‌ی کد
    // ---------------------------------------------------------------

    [Fact]
    public async Task ResendingTheCode_WorksForMembersOfTheCompany_Only()
    {
        await ArrangeAsync();

        var staff = await AddAsync(Request(Mobile, "سارا", "رضایی", null, null));
        _otp.RequestedMobiles.Clear();

        var sent = await SendCodeAsync(staff.UserId);
        Assert.True(sent.Accepted);
        Assert.Equal("0912***6789", sent.MaskedMobile);
        Assert.Equal(new[] { Mobile }, _otp.RequestedMobiles);

        // پیامک ردشده (محدودیت ارسال) = Accepted: false، نه خطا
        _otp.Accept = false;
        Assert.False((await SendCodeAsync(staff.UserId)).Accepted);
    }

    [Fact]
    public async Task ResendingTheCode_CannotTargetSomeoneOutsideTheCompany()
    {
        await ArrangeAsync();

        // «stranger» فقط عضو شرکت B است؛ از شرکت A نباید بشود به او کد فرستاد (پیامک‌بمباران/آزار)
        await AddUserWithPhoneAsync("stranger", "09120000020", confirmed: true);
        await _data.AddMembershipAsync("stranger", CompanyB);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SendCodeAsync("stranger"));
        Assert.Contains("عضو شرکت نیست", ex.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => SendCodeAsync("nobody-at-all"));

        Assert.Empty(_otp.RequestedMobiles);
    }

    [Fact]
    public async Task ResendingTheCode_RequiresAnActiveAccountWithAConfirmedMobile()
    {
        await ArrangeAsync();

        await AddUserWithPhoneAsync("inactive", "09120000021", confirmed: true, active: false);
        await _data.AddMembershipAsync("inactive", CompanyA);
        var inactive = await Assert.ThrowsAsync<InvalidOperationException>(() => SendCodeAsync("inactive"));
        Assert.Contains("غیرفعال", inactive.Message);

        await AddUserWithPhoneAsync("unconfirmed", "09120000022", confirmed: false);
        await _data.AddMembershipAsync("unconfirmed", CompanyA);
        var unconfirmed = await Assert.ThrowsAsync<InvalidOperationException>(() => SendCodeAsync("unconfirmed"));
        Assert.Contains("تأیید نشده", unconfirmed.Message);

        Assert.Empty(_otp.RequestedMobiles);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static MemberCreateRequest Request(
        string mobile, string first, string last, string? title, string? extension, params int[] roleIds)
        => new()
        {
            Mobile = mobile,
            FirstName = first,
            LastName = last,
            JobTitle = title,
            InternalPhone = extension,
            RoleIds = roleIds.ToList()
        };

    private async Task<MemberCreateResult> AddAsync(MemberCreateRequest request, int tenantId = CompanyA)
    {
        using var scope = _provider.CreateScope();
        return await NewService(scope).AddMemberAsync(tenantId, request);
    }

    private async Task<StaffPasswordCodeResult> SendCodeAsync(string userId, int tenantId = CompanyA)
    {
        using var scope = _provider.CreateScope();
        return await NewService(scope).SendPasswordCodeAsync(tenantId, userId);
    }

    private TenantStaffService NewService(IServiceScope scope)
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        return new TenantStaffService(context, users, new TenantSecurityService(context), _otp);
    }

    private async Task AddUserWithPhoneAsync(
        string id, string mobile, bool confirmed, bool active = true, string? fullName = null)
    {
        await using var context = _db.NewContext();
        context.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = mobile,
            NormalizedUserName = mobile,
            PhoneNumber = mobile,
            PhoneNumberConfirmed = confirmed,
            FullName = fullName ?? $"کاربر {id}",
            IsActive = active
        });
        await context.SaveChangesAsync();
    }
}
