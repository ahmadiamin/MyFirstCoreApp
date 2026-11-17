using MyFirstCoreApp.Application.Security;
using Xunit;

namespace MyFirstCoreApp.Tests.Common;

public class PermissionCatalogTests
{
    [Theory]
    [InlineData("Permission.View")]
    [InlineData("Permission.Edit")]
    [InlineData("Menu.View")]
    [InlineData("Menu.Create")]
    [InlineData("permission.view")]        // حروف کوچک هم سراسری است
    public void PlatformPermissions_AreRecognised(string name)
    {
        Assert.True(PermissionCatalog.IsPlatform(name));
        Assert.False(PermissionCatalog.IsTenantScope(name));
        Assert.False(PermissionCatalog.IsAccountLevel(name));
    }

    [Theory]
    [InlineData("Tenancy.View")]
    [InlineData("Tenancy.Create")]
    [InlineData("Tenancy.Select")]
    [InlineData("tenancy.create")]
    public void AccountLevelPermissions_AreRecognised(string name)
    {
        Assert.True(PermissionCatalog.IsAccountLevel(name));
        Assert.False(PermissionCatalog.IsTenantScope(name));
        Assert.False(PermissionCatalog.IsPlatform(name));
    }

    [Theory]
    [InlineData("Invoice.View")]
    [InlineData("Invoice.Create")]
    [InlineData("Report.Ledger")]
    [InlineData("User.View")]           // کاربران و نقش‌ها حالا مخصوص هر شرکت‌اند
    [InlineData("Role.Edit")]
    [InlineData("Tenancy.Edit")]        // ویرایش شرکت: سطح شرکت (مالک)
    [InlineData("Tenancy.Delete")]
    public void EverythingElse_IsTenantScope(string name)
    {
        Assert.True(PermissionCatalog.IsTenantScope(name));
        Assert.False(PermissionCatalog.IsPlatform(name));
        Assert.False(PermissionCatalog.IsAccountLevel(name));
    }

    [Theory]
    [InlineData("PermissionsReport.View")]    // فقط «پیشوندِ نقطه‌دار» سراسری است
    [InlineData("Menus.View")]
    public void PrefixMatching_RequiresTheDot(string name)
        => Assert.False(PermissionCatalog.IsPlatform(name));

    [Fact]
    public void EachPermission_BelongsToExactlyOneScope()
    {
        foreach (var name in new[] { "Permission.View", "Menu.Edit", "Tenancy.Create", "Tenancy.Edit", "Invoice.View", "User.View" })
        {
            var scopes = new[]
            {
                PermissionCatalog.IsPlatform(name),
                PermissionCatalog.IsAccountLevel(name),
                PermissionCatalog.IsTenantScope(name)
            };

            Assert.Equal(1, scopes.Count(s => s));
        }
    }
}
