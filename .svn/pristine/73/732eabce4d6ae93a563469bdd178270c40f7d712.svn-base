using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Inventory;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Inventory;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Barcode).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(2000);

        // قیمت‌ها
        builder.Property(x => x.LastPurchasePrice).HasPrecision(18, 0);
        builder.Property(x => x.SalePrice).HasPrecision(18, 0);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);

        // رابطه با دسته
        builder.HasOne(x => x.ProductCategory)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.ProductCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با واحد
        builder.HasOne(x => x.Unit)
            .WithMany()
            .HasForeignKey(x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.Code });
        builder.HasIndex(x => x.IsActive);
    }
}