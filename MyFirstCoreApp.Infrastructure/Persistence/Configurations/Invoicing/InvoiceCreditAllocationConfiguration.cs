using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Invoicing;

public class InvoiceCreditAllocationConfiguration
    : IEntityTypeConfiguration<InvoiceCreditAllocation>
{
    public void Configure(EntityTypeBuilder<InvoiceCreditAllocation> builder)
    {
        builder.ToTable("InvoiceCreditAllocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 0);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.HasOne(x => x.CreditInvoice)
            .WithMany(x => x.IssuedCreditAllocations)
            .HasForeignKey(x => x.CreditInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetInvoice)
            .WithMany(x => x.ReceivedCreditAllocations)
            .HasForeignKey(x => x.TargetInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.CreditInvoiceId);
        builder.HasIndex(x => x.TargetInvoiceId);
    }
}