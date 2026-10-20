using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Banking;

public class PaymentHistoryConfiguration : IEntityTypeConfiguration<PaymentHistory>
{
    public void Configure(EntityTypeBuilder<PaymentHistory> builder)
    {
        builder.ToTable("PaymentHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.UserId).HasMaxLength(450);

        builder.HasOne(x => x.Payment)
            .WithMany(p => p.Histories)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.PaymentId);
        builder.HasIndex(x => new { x.TenantId, x.Date });
    }
}