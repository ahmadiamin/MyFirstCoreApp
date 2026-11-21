using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Projects;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Projects;

public class ProjectStatusHistoryConfiguration : IEntityTypeConfiguration<ProjectStatusHistory>
{
    public void Configure(EntityTypeBuilder<ProjectStatusHistory> builder)
    {
        builder.ToTable("ProjectStatusHistories");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.ChangedByUserId).HasMaxLength(450);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.StatusHistories)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.ChangedAt });
        builder.HasIndex(x => new { x.TenantId, x.ChangedByUserId });
    }
}
