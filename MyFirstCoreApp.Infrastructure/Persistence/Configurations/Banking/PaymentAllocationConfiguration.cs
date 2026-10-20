using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Banking;

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasOne(x => x.Payment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.PaymentId);
        builder.HasIndex(x => x.InvoiceId);
    }
}