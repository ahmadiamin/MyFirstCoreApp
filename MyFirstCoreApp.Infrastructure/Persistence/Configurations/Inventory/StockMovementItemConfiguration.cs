using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Inventory;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Inventory;

public class StockMovementItemConfiguration
    : IEntityTypeConfiguration<StockMovementItem>
{
    public void Configure(EntityTypeBuilder<StockMovementItem> builder)
    {
        builder.ToTable("StockMovementItems", table =>
        {
            table.HasCheckConstraint(
                "CK_StockMovementItems_Quantity",
                "[Quantity] > 0"
            );
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasPrecision(18, 0);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.StockMovementId });

        builder.HasIndex(x => x.ProductId);
    }
}