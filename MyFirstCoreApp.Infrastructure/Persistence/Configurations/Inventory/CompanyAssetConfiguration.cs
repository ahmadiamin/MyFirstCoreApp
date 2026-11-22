using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Inventory;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Inventory;

public sealed class CompanyAssetConfiguration : IEntityTypeConfiguration<CompanyAsset>
{
    public void Configure(EntityTypeBuilder<CompanyAsset> builder)
    {
        builder.ToTable("CompanyAssets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AcquisitionCost).HasPrecision(18, 0);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Assignments).WithOne(x => x.CompanyAsset).HasForeignKey(x => x.CompanyAssetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TenantId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.SerialNumber }).IsUnique().HasFilter("[SerialNumber] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.ProductId });
    }
}
