using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Domain.Entities.Identity;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Projects;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class CompanyAssetServiceTests : IDisposable
{
    private const int TenantId = 41;
    private readonly TestDatabase _database = new();

    public CompanyAssetServiceTests() => _database.Tenant.TenantId = TenantId;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task AssetCanBeAssignedToEmployeeThenReturnedWithHistoryPreserved()
    {
        int productId;
        int membershipId;
        await using (var context = _database.NewContext())
        {
            context.Tenants.Add(new Tenant { Id = TenantId, Name = "شرکت تست" });
            context.Units.Add(new Unit { TenantId = TenantId, Name = "عدد", Symbol = "عدد", IsActive = true });
            await context.SaveChangesAsync();
            var product = new Product
            {
                TenantId = TenantId,
                Name = "دریل شارژی",
                UnitId = await context.Units.Select(x => x.Id).SingleAsync(),
                ProductType = ProductType.Tool,
                IsSaleable = false,
                TrackInventory = true,
                IsActive = true
            };
            var user = new ApplicationUser { Id = "employee-1", UserName = "employee-1", FullName = "کارمند تست", IsActive = true };
            context.Users.Add(user);
            context.Products.Add(product);
            await context.SaveChangesAsync();
            var membership = new UserTenant { TenantId = TenantId, UserId = user.Id, FirstName = "کارمند", LastName = "تست" };
            context.UserTenants.Add(membership);
            await context.SaveChangesAsync();
            productId = product.Id;
            membershipId = membership.Id;
        }

        await using var serviceContext = _database.NewContext();
        var service = new CompanyAssetService(serviceContext);
        var assetId = await service.CreateAsync(new CompanyAssetRequest
        {
            ProductId = productId,
            SerialNumber = "DRILL-001",
            AcquisitionDate = new DateOnly(2026, 1, 1),
            AcquisitionCost = 2_500_000
        }, TenantId);
        var asset = await service.GetByIdAsync(assetId, TenantId);
        Assert.NotNull(asset);
        Assert.Equal("AST-000001", asset.AssetTag);

        var assignmentId = await service.AssignAsync(new CompanyAssetAssignmentRequest
        {
            CompanyAssetId = assetId,
            UserTenantId = membershipId,
            AssignedDate = new DateOnly(2026, 1, 2),
            AssignedByUserId = "owner-1",
            Notes = "تحویل برای اجرای کار"
        }, TenantId);

        var assigned = await service.GetByIdAsync(assetId, TenantId);
        Assert.NotNull(assigned);
        Assert.Equal(CompanyAssetStatus.Assigned, assigned.Status);
        Assert.Equal("کارمند تست", assigned.AssignedUserName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignAsync(
            new CompanyAssetAssignmentRequest { CompanyAssetId = assetId, AssignedDate = new DateOnly(2026, 1, 3) }, TenantId));

        await service.ReturnAsync(new CompanyAssetReturnRequest
        {
            AssignmentId = assignmentId,
            ReturnedDate = new DateOnly(2026, 1, 5),
            ReturnedByUserId = "owner-1",
            ReturnNotes = "سالم تحویل شد"
        }, TenantId);

        var returned = await service.GetByIdAsync(assetId, TenantId);
        Assert.NotNull(returned);
        Assert.Equal(CompanyAssetStatus.Available, returned.Status);
        var history = Assert.Single(returned.Assignments);
        Assert.Equal(new DateOnly(2026, 1, 5), history.ReturnedDate);
        Assert.Equal("سالم تحویل شد", history.ReturnNotes);

        await service.DeleteAsync(assetId, TenantId, "owner-1");
        Assert.Empty(await service.GetListAsync(TenantId));
        var deletedAsset = await serviceContext.CompanyAssets.IgnoreQueryFilters()
            .Include(x => x.Assignments).SingleAsync(x => x.Id == assetId);
        Assert.True(deletedAsset.IsDeleted);
        Assert.Single(deletedAsset.Assignments);
    }

    [Fact]
    public async Task CannotRegisterRegularSaleableProductAsIndividualCompanyAsset()
    {
        int productId;
        await using (var context = _database.NewContext())
        {
            context.Units.Add(new Unit { TenantId = TenantId, Name = "عدد", Symbol = "عدد", IsActive = true });
            await context.SaveChangesAsync();
            var product = new Product
            {
                TenantId = TenantId,
                Name = "کالای فروشگاهی",
                UnitId = await context.Units.Select(x => x.Id).SingleAsync(),
                ProductType = ProductType.Goods,
                IsActive = true
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            productId = product.Id;
        }

        await using var serviceContext = _database.NewContext();
        var service = new CompanyAssetService(serviceContext);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            new CompanyAssetRequest { ProductId = productId }, TenantId));
        Assert.Contains("ابزار یا دارایی ثابت", error.Message);
    }
}
