using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Projects;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Projects;

public class ProjectStageConfiguration : IEntityTypeConfiguration<ProjectStage>
{
    public void Configure(EntityTypeBuilder<ProjectStage> builder)
    {
        builder.ToTable("ProjectStages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.WaitingReason)
            .HasMaxLength(1000);

        builder.Property(x => x.Notes)
            .HasMaxLength(2000);

        builder.HasOne(x => x.AssignedUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedUserTenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Histories)
            .WithOne(x => x.ProjectStage)
            .HasForeignKey(x => x.ProjectStageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.SortOrder });

        builder.HasIndex(x => new { x.TenantId, x.Status });

        builder.HasIndex(x => new { x.TenantId, x.AssignedUserTenantId });

        builder.HasIndex(x => new { x.TenantId, x.NextFollowUpDate });
    }
}