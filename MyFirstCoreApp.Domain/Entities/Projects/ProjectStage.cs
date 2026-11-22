using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Projects;

public class ProjectStage : TenantEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsRequired { get; set; } = true;

    public ProjectStageStatus Status { get; set; } = ProjectStageStatus.NotStarted;

    public int? AssignedUserTenantId { get; set; }
    public UserTenant? AssignedUser { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? WaitingReason { get; set; }

    public DateOnly? NextFollowUpDate { get; set; }

    public string? Notes { get; set; }

    public ICollection<ProjectStageHistory> Histories { get; set; } = new List<ProjectStageHistory>();
    public ICollection<ProjectStageReport> Reports { get; set; } = new List<ProjectStageReport>();
}
