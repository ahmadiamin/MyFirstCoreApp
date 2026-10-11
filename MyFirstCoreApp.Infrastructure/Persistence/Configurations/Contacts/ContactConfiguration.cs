using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Contacts;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Contacts;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Code).HasMaxLength(50);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Mobile).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(1000);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.NationalId).HasMaxLength(20);
        builder.Property(x => x.EconomicCode).HasMaxLength(30);
        builder.Property(x => x.RegistrationNumber).HasMaxLength(30);
        builder.Property(x => x.Notes).HasMaxLength(2000);

        // مالی
        builder.Property(x => x.CurrentBalance).HasPrecision(18, 0);
        builder.Property(x => x.CreditLimit).HasPrecision(18, 0);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => x.ContactType);
        builder.HasIndex(x => x.IsActive);
    }
}