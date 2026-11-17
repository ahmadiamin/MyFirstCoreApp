using MyFirstCoreApp.Application.Security;
using MyFirstCoreApp.Infrastructure.Security;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;
using static MyFirstCoreApp.Tests.TestSupport.SecurityTestHelper;

namespace MyFirstCoreApp.Tests.Services;

/// <summary>
/// «آیا این دسترسی در سمتِ هر شرکتی استفاده شده؟»؛ برای جلوگیری از تغییر نامِ دسترسیِ مورداستفاده
/// (سمت‌ها دسترسی را با نام نگه می‌دارند و تغییر نام، دسترسی افراد را بی‌صدا قطع می‌کرد).
/// </summary>
public class PermissionInUseTests : IDisposable
{
    private const int CompanyA = 1;
    private const int CompanyB = 2;

    private readonly TestDatabase _db = new();
    private readonly SecurityTestHelper _data;

    public PermissionInUseTests()
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
    }

    [Fact]
    public async Task AnUnusedPermission_IsNotInUse()
    {
        await ArrangeAsync();

        Assert.False(await InUseAsync(InvoiceView));
        Assert.False(await InUseAsync("Nothing.Here"));
    }

    [Fact]
    public async Task APermissionInACompanyRole_IsInUse()
    {
        await ArrangeAsync();
        await CreateRoleAsync(CompanyA, "منشی", InvoiceView);

        Assert.True(await InUseAsync(InvoiceView));
        Assert.False(await InUseAsync(InvoiceCreate));          // فقط همان که در نقش است
    }

    [Fact]
    public async Task UsageInAnotherCompany_Counts_EvenWhenTheActiveCompanyIsDifferent()
    {
        await ArrangeAsync();
        await CreateRoleAsync(CompanyB, "انباردار", WarehouseView);

        // شرکت فعال A است؛ ولی کاتالوگ سراسری است و استفاده‌ی شرکت B هم باید دیده شود
        _db.Tenant.TenantId = CompanyA;
        Assert.True(await InUseAsync(WarehouseView));
    }

    [Fact]
    public async Task OnceNoRoleUsesIt_ItIsFreeAgain()
    {
        await ArrangeAsync();
        var first = await CreateRoleAsync(CompanyA, "منشی", InvoiceView, InvoiceCreate);
        var second = await CreateRoleAsync(CompanyA, "فروش", InvoiceView);

        Assert.True(await InUseAsync(InvoiceView));

        // برداشتن از یک نقش: هنوز نقش دیگر استفاده می‌کند
        await UpdateRoleAsync(first, "منشی", InvoiceCreate);
        Assert.True(await InUseAsync(InvoiceView));

        // حذف نقشِ دوم: دیگر کسی استفاده نمی‌کند
        await using (var context = _db.NewContext())
            await new TenantSecurityService(context).DeleteRoleAsync(CompanyA, second);

        Assert.False(await InUseAsync(InvoiceView));
        Assert.True(await InUseAsync(InvoiceCreate));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private async Task<bool> InUseAsync(string permission)
    {
        await using var context = _db.NewContext();
        return await new TenantSecurityService(context).IsPermissionInUseAsync(permission);
    }

    private async Task<int> CreateRoleAsync(int tenantId, string name, params string[] permissions)
    {
        var previous = _db.Tenant.TenantId;
        _db.Tenant.TenantId = tenantId;

        try
        {
            await using var context = _db.NewContext();
            return await new TenantSecurityService(context).CreateRoleAsync(tenantId,
                new TenantRoleRequest { Name = name, PermissionNames = permissions.ToList() });
        }
        finally
        {
            _db.Tenant.TenantId = previous;
        }
    }

    private async Task UpdateRoleAsync(int roleId, string name, params string[] permissions)
    {
        await using var context = _db.NewContext();
        await new TenantSecurityService(context).UpdateRoleAsync(CompanyA, roleId,
            new TenantRoleRequest { Name = name, PermissionNames = permissions.ToList() });
    }
}
