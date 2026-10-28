using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class SystemSmsSettingConfiguration : IEntityTypeConfiguration<SystemSmsSetting>
{
    public void Configure(EntityTypeBuilder<SystemSmsSetting> builder)
    {
        builder.ToTable("SystemSmsSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProtectedApiKey).HasMaxLength(2000);
        builder.Property(x => x.LineNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RegistrationSmsPatternCode).HasMaxLength(100);
    }
}
