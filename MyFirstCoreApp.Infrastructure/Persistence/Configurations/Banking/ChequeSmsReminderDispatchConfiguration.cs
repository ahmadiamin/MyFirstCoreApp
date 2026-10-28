using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Banking;

public sealed class ChequeSmsReminderDispatchConfiguration : IEntityTypeConfiguration<ChequeSmsReminderDispatch>
{
    public void Configure(EntityTypeBuilder<ChequeSmsReminderDispatch> builder)
    {
        builder.ToTable("ChequeSmsReminderDispatches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderRequestId).HasMaxLength(100);
        builder.Property(x => x.RecipientMobile).HasMaxLength(30);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.ChequeId, x.DueDate, x.Kind }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasOne<Cheque>().WithMany().HasForeignKey(x => x.ChequeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
