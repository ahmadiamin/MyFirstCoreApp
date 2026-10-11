using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Banking;

public class ChequeHistoryConfiguration : IEntityTypeConfiguration<ChequeHistory>
{
    public void Configure(EntityTypeBuilder<ChequeHistory> builder)
    {
        builder.ToTable("ChequeHistories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.UserId).HasMaxLength(450);

        builder.HasOne(x => x.Cheque)
            .WithMany(c => c.Histories)
            .HasForeignKey(x => x.ChequeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.ChequeId);
        builder.HasIndex(x => new { x.TenantId, x.Date });
    }
}