using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Accounting;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Accounting;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(1000);

        // اطلاعات بانکی
        builder.Property(x => x.BankName).HasMaxLength(200);
        builder.Property(x => x.AccountNumber).HasMaxLength(50);
        builder.Property(x => x.CardNumber).HasMaxLength(30);
        builder.Property(x => x.ShebaNumber).HasMaxLength(30);

        // مالی
        builder.Property(x => x.CurrentBalance).HasPrecision(18, 0);

        // رابطه با Category
        builder.HasOne(x => x.AccountCategory)
            .WithMany(c => c.Accounts)
            .HasForeignKey(x => x.AccountCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // درخت
        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => x.AccountCategoryId);
        builder.HasIndex(x => x.IsActive);
    }
}