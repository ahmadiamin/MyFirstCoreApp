using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Inventory;

public class StockMovementConfiguration
    : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", table =>
        {
            table.HasCheckConstraint(
                "CK_StockMovements_Warehouses",
                """
                (
                    [MovementType] IN (1, 2)
                    AND [SourceWarehouseId] IS NULL
                    AND [DestinationWarehouseId] IS NOT NULL
                )
                OR
                (
                    [MovementType] = 3
                    AND [SourceWarehouseId] IS NOT NULL
                    AND [DestinationWarehouseId] IS NULL
                )
                OR
                (
                    [MovementType] = 4
                    AND [SourceWarehouseId] IS NOT NULL
                    AND [DestinationWarehouseId] IS NOT NULL
                    AND [SourceWarehouseId] <> [DestinationWarehouseId]
                )
                """
            );
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Date)
            .IsRequired();

        builder.Property(x => x.MovementType)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.HasOne(x => x.SourceWarehouse)
            .WithMany(x => x.SourceMovements)
            .HasForeignKey(x => x.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DestinationWarehouse)
            .WithMany(x => x.DestinationMovements)
            .HasForeignKey(x => x.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReferenceInvoice)
            .WithMany()
            .HasForeignKey(x => x.ReferenceInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.StockMovement)
            .HasForeignKey(x => x.StockMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.Number })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.Date });

        builder.HasIndex(x => new { x.TenantId, x.Status, x.Date });

        builder.HasIndex(x => x.SourceWarehouseId);

        builder.HasIndex(x => x.DestinationWarehouseId);

        builder.HasOne(x => x.ReversesMovement)
            .WithMany(x => x.ReversalMovements)
            .HasForeignKey(x => x.ReversesMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ReversesMovementId)
            .IsUnique()
            .HasFilter("[ReversesMovementId] IS NOT NULL AND [IsDeleted] = 0");
    }
}