using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Invoicing;

public class InvoiceBehaviorSettingConfiguration
    : IEntityTypeConfiguration<InvoiceBehaviorSetting>
{
    public void Configure(EntityTypeBuilder<InvoiceBehaviorSetting> builder)
    {
        builder.ToTable("InvoiceBehaviorSettings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}