using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Identity;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Identity;

public sealed class PasswordResetOtpConfiguration
    : IEntityTypeConfiguration<PasswordResetOtp>
{
    public void Configure(EntityTypeBuilder<PasswordResetOtp> builder)
    {
        builder.ToTable("PasswordResetOtps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(x => x.CodeHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.UserId,
            x.IsDeleted,
            x.ConsumedAt,
            x.ExpiresAt
        });
        builder.HasIndex(x => new { x.UserId, x.SentAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}