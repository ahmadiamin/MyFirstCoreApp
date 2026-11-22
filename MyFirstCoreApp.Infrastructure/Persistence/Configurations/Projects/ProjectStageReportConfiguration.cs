using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyFirstCoreApp.Domain.Entities.Projects;

namespace MyFirstCoreApp.Infrastructure.Persistence.Configurations.Projects;

public sealed class ProjectStageReportConfiguration : IEntityTypeConfiguration<ProjectStageReport>
{
    public void Configure(EntityTypeBuilder<ProjectStageReport> builder)
    {
        builder.ToTable("ProjectStageReports", table =>
            table.HasCheckConstraint("CK_ProjectStageReports_ProgressPercent", "[ProgressPercent] BETWEEN 0 AND 100"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WorkSummary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.StopReason).HasMaxLength(1000);
        builder.Property(x => x.ReportedByUserId).HasMaxLength(450);
        builder.HasOne(x => x.ProjectStage).WithMany(x => x.Reports)
            .HasForeignKey(x => x.ProjectStageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TenantId, x.ProjectStageId, x.ReportDate });
    }
}
