using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Accounting;

public class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("FiscalYears");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ClosedBy).HasMaxLength(450);

        // فیلتر سراسری (شرکت + حذف نرم) در ApplicationDbContext اعمال می‌شود.

        // هر شرکت فقط یک سال مالی با یک تاریخ شروع (جلوگیری از تکرار؛ همپوشانی را سرویس بررسی می‌کند)
        builder.HasIndex(x => new { x.TenantId, x.StartDate })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
