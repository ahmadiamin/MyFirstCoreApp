using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Invoicing;

public class InvoiceHistoryConfiguration : IEntityTypeConfiguration<InvoiceHistory>
{
    public void Configure(EntityTypeBuilder<InvoiceHistory> builder)
    {
        builder.ToTable("InvoiceHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.UserId).HasMaxLength(450);

        // رابطه با Invoice
        builder.HasOne(x => x.Invoice)
            .WithMany(i => i.Histories)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        // ایندکس‌ها
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => new { x.TenantId, x.Date });
    }
}