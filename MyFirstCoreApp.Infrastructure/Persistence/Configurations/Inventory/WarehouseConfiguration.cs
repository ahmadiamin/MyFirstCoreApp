using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Inventory;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Inventory;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("Warehouses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Address)
            .HasMaxLength(1000);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsActive] = 1 AND [IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.Name });

        builder.HasIndex(x => x.IsActive);
    }
}