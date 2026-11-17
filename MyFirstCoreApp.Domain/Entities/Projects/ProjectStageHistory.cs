using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Projects;

public class ProjectStageHistory : TenantEntity
{
    public int ProjectStageId { get; set; }
    public ProjectStage ProjectStage { get; set; } = null!;

    public ProjectStageStatus? PreviousStatus { get; set; }

    public ProjectStageStatus NewStatus { get; set; }

    public string? Description { get; set; }

    // شناسه کاربری که تغییر را ثبت کرده است.
    public string? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}