using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Invoicing
{
    public class InvoicePrintSettingConfiguration : IEntityTypeConfiguration<InvoicePrintSetting>
    {
        public void Configure(EntityTypeBuilder<InvoicePrintSetting> builder)
        {
            builder.ToTable("InvoicePrintSettings");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PoemSelectionMode).HasConversion<int>();

            builder.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(2000);

            builder.HasOne(x => x.SelectedInvoicePoem)
                .WithMany()
                .HasForeignKey(x => x.SelectedInvoicePoemId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
