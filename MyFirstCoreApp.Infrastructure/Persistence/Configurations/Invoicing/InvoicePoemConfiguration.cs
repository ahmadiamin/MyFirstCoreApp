using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Invoicing
{
    public class InvoicePoemConfiguration : IEntityTypeConfiguration<InvoicePoem>
    {
        public void Configure(EntityTypeBuilder<InvoicePoem> builder)
        {
            {
                builder.ToTable("InvoicePoems");
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Text)
                    .IsRequired()
                    .HasMaxLength(2000);
                
                builder.Property(x => x.IsActive)
                    .HasDefaultValue(true);
            }
        }
    }
}