using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Projects;

/// <summary>A dated execution update for a project stage.</summary>
public sealed class ProjectStageReport : TenantEntity
{
    public int ProjectStageId { get; set; }
    public ProjectStage ProjectStage { get; set; } = null!;
    public DateOnly ReportDate { get; set; }
    public int ProgressPercent { get; set; }
    public string WorkSummary { get; set; } = string.Empty;
    public bool IsStopped { get; set; }
    public string? StopReason { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string? ReportedByUserId { get; set; }
}
