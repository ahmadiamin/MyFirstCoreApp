using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Contacts;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Contacts;

public sealed class CustomerPortalShareConfiguration : IEntityTypeConfiguration<CustomerPortalShare>
{
    public void Configure(EntityTypeBuilder<CustomerPortalShare> builder)
    {
        builder.ToTable("CustomerPortalShares");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProtectedToken).HasMaxLength(1000);
        builder.Property(x => x.LastSmsRequestId).HasMaxLength(100);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ContactId, x.RevokedAtUtc });
        builder.HasOne(x => x.Contact)
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
