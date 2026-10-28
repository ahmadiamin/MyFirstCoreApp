using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Tenancy;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Tenancy;

public sealed class TenantSmsSettingConfiguration : IEntityTypeConfiguration<TenantSmsSetting>
{
    public void Configure(EntityTypeBuilder<TenantSmsSetting> builder)
    {
        builder.ToTable("TenantSmsSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProtectedApiKey).HasMaxLength(2000);
        builder.Property(x => x.LineNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PortalLinkText).HasMaxLength(1000);
        builder.Property(x => x.ChequeReminderText).HasMaxLength(1000);
        builder.Property(x => x.ChequeReminderTime).HasDefaultValue(new TimeOnly(9, 0));
        builder.Property(x => x.ChequeReminderMobile).HasMaxLength(30);
        builder.HasIndex(x => x.TenantId).IsUnique();
        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
