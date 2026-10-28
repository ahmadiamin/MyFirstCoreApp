using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Attachments;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Attachments
{
    public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.ToTable("Attachments");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.FileName).IsRequired().HasMaxLength(200);
            builder.Property(x => x.StoredPath).IsRequired().HasMaxLength(300);
            builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Description).HasMaxLength(500);

            // فیلتر سراسری (شرکت + حذف نرم) در ApplicationDbContext اعمال می‌شود.

            builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
        }
    }
}