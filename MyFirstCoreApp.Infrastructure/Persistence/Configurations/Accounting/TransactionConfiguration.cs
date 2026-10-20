using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Accounting;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Reference).HasMaxLength(200);
        builder.Property(x => x.AttachmentPath).HasMaxLength(500);

        builder.Property(x => x.TotalAmount).HasPrecision(18, 0);

        // رابطه با TransactionGroup
        builder.HasOne(x => x.TransactionGroup)
            .WithMany()
            .HasForeignKey(x => x.TransactionGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // رابطه با سال مالی
        builder.HasOne(x => x.FiscalYear)
            .WithMany()
            .HasForeignKey(x => x.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلتر سراسری (شرکت + حذف نرم) در ApplicationDbContext اعمال می‌شود؛
        // خط قبلی HasQueryFilter(!IsDeleted) فقط حذف نرم را می‌پوشاند و حذف شد.

        // ایندکس‌ها
        builder.HasIndex(x => new { x.TenantId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");   // فقط رکوردهای Active

        builder.HasIndex(x => new { x.TenantId, x.Date });
        builder.HasIndex(x => new { x.TenantId, x.FiscalYearId });
        builder.HasIndex(x => x.TransactionGroupId);
        builder.HasIndex(x => x.IsPosted);

        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
    }
}