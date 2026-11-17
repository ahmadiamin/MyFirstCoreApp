using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Contacts;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Contacts;

public class ContactPersonConfiguration : IEntityTypeConfiguration<ContactPerson>
{
    public void Configure(EntityTypeBuilder<ContactPerson> builder)
    {
        builder.ToTable("ContactPersons");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Position).HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasOne(x => x.Contact)
            .WithMany(x => x.ContactPersons)
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TenantId, x.ContactId, x.IsActive });
    }
}
