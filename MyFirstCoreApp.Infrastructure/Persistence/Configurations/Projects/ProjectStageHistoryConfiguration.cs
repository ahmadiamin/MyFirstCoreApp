using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Projects;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Projects;

public class ProjectStageHistoryConfiguration : IEntityTypeConfiguration<ProjectStageHistory>
{
    public void Configure(EntityTypeBuilder<ProjectStageHistory> builder)
    {
        builder.ToTable("ProjectStageHistories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.ChangedByUserId)
            .HasMaxLength(450);

        builder.HasIndex(x => new { x.TenantId, x.ProjectStageId, x.ChangedAt });

        builder.HasIndex(x => new { x.TenantId, x.ChangedByUserId });
    }
}