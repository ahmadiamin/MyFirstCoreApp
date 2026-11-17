using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Contacts;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Contacts;

public class ContactPersonNumberConfiguration : IEntityTypeConfiguration<ContactPersonNumber>
{
    public void Configure(EntityTypeBuilder<ContactPersonNumber> builder)
    {
        builder.ToTable("ContactPersonNumbers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Label).HasMaxLength(50);
        builder.Property(x => x.Number).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Extension).HasMaxLength(10);

        builder.HasOne(x => x.ContactPerson)
            .WithMany(x => x.Numbers)
            .HasForeignKey(x => x.ContactPersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.ContactPersonId, x.NumberType });
    }
}
