using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Accounting;

public class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLine>
{
    public void Configure(EntityTypeBuilder<TransactionLine> builder)
    {
        builder.ToTable("TransactionLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Debit).HasPrecision(18, 0);
        builder.Property(x => x.Credit).HasPrecision(18, 0);

        // رابطه با Transaction
        builder.HasOne(x => x.Transaction)
            .WithMany(t => t.Lines)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        // رابطه با Account
        builder.HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با Contact
        builder.HasOne(x => x.Contact)
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        // ایندکس‌ها
        builder.HasIndex(x => x.TransactionId);
        builder.HasIndex(x => new { x.TenantId, x.AccountId });
        builder.HasIndex(x => x.ContactId);
    }
}