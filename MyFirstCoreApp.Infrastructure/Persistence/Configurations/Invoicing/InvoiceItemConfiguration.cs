using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Invoicing;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Invoicing;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(1000);

        // 🆕 تعداد: می‌تونه اعشار داشته باشه (کیلو، متر)
        builder.Property(x => x.Quantity).HasPrecision(18, 3);

        // 🆕 مبالغ: بدون اعشار
        builder.Property(x => x.UnitPrice).HasPrecision(18, 0);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 0);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 0);
        builder.Property(x => x.LineTotal).HasPrecision(18, 0);

        // 🆕 نرخ‌ها: با اعشار (درصد)
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);

        // رابطه با Invoice
        builder.HasOne(x => x.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // رابطه با Product
        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // فیلتر Tenant
        builder.HasQueryFilter(x => !x.IsDeleted);

        // ایندکس‌ها
        builder.HasIndex(x => x.InvoiceId);
        builder.HasIndex(x => x.ProductId);
        builder.HasIndex(x => x.WarehouseId);
    }
}
