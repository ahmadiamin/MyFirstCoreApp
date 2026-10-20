using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Invoicing;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(2000);

        // مالی (بدون اعشار)
        builder.Property(x => x.SubTotal).HasPrecision(18, 0);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 0);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 0);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 0);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 0);

        // رابطه با Contact
        builder.HasOne(x => x.Contact)
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با ReferenceInvoice (پیش‌فاکتور)
        builder.HasOne(x => x.ReferenceInvoice)
            .WithMany()
            .HasForeignKey(x => x.ReferenceInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با Transaction (سند حسابداری)
        builder.HasOne(x => x.Transaction)
            .WithMany()
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        // ایندکس‌ها
        builder.HasIndex(x => new { x.TenantId, x.InvoiceType, x.Number })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.TenantId, x.Date });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ContactId);

        // 🆕 گردش کار پیش‌فاکتور
        builder.Property(x => x.CustomerResponse).HasMaxLength(2000);
        builder.Property(x => x.RevisionCount).HasDefaultValue(0);

        // 🆕 ایندکس‌های گردش کار
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.NextFollowUpDate);
        builder.HasIndex(x => x.SentDate);
        builder.HasIndex(x => x.ApprovedDate);
    }
}