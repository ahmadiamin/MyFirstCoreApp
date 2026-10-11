using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Banking;

public class ChequeConfiguration : IEntityTypeConfiguration<Cheque>
{
    public void Configure(EntityTypeBuilder<Cheque> builder)
    {
        builder.ToTable("Cheques");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChequeNumber).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Amount).HasPrecision(18, 0);
        builder.Property(x => x.BankName).HasMaxLength(200);
        builder.Property(x => x.AccountNumber).HasMaxLength(50);
        builder.Property(x => x.BranchCode).HasMaxLength(20);
        builder.Property(x => x.BranchName).HasMaxLength(200);
        builder.Property(x => x.DrawerName).HasMaxLength(200);
        builder.Property(x => x.DrawerNationalId).HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.BounceReason).HasMaxLength(2000);
        builder.Property(x => x.ImagePath).HasMaxLength(500);
        builder.Property(x => x.BankTrackingNumber).HasMaxLength(100);

        // روابط
        builder.HasOne(x => x.Contact)
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BankAccount)
            .WithMany()
            .HasForeignKey(x => x.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Invoice)
            .WithMany()
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        // ایندکس‌ها
        builder.HasIndex(x => new { x.TenantId, x.ChequeNumber });
        builder.HasIndex(x => new { x.TenantId, x.DueDate });
        builder.HasIndex(x => x.Direction);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ContactId);
        builder.HasIndex(x => x.InvoiceId);
    }
}