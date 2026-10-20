using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Banking;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
	public void Configure(EntityTypeBuilder<Payment> builder)
	{
		builder.ToTable("Payments");
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Reference).HasMaxLength(200);
		builder.Property(x => x.Description).HasMaxLength(2000);
		builder.Property(x => x.ImagePath).HasMaxLength(500);

		// مالی
		builder.Property(x => x.Amount).HasPrecision(18, 0);
		builder.Property(x => x.AllocatedAmount).HasPrecision(18, 0);

		// روابط
		builder.HasOne(x => x.Contact)
			.WithMany()
			.HasForeignKey(x => x.ContactId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.BankAccount)
			.WithMany()
			.HasForeignKey(x => x.BankAccountId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.Cheque)
			.WithMany()
			.HasForeignKey(x => x.ChequeId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.Transaction)
			.WithMany()
			.HasForeignKey(x => x.TransactionId)
			.OnDelete(DeleteBehavior.Restrict);

		// فیلتر Tenant
		builder.HasQueryFilter(x => !x.IsDeleted);

		// 🆕 ایندکس‌ها
		builder.HasIndex(x => new { x.TenantId, x.Number })
			.IsUnique()
			.HasFilter("[IsDeleted] = 0");   // 🆕

		builder.HasIndex(x => new { x.TenantId, x.Date });
		builder.HasIndex(x => x.PaymentType);
		builder.HasIndex(x => x.Status);
		builder.HasIndex(x => x.ContactId);
		builder.HasIndex(x => x.BankAccountId);
		builder.HasIndex(x => x.ChequeId);
	}
}