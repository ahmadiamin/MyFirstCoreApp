using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Projects;

public class ProjectStatusHistory : TenantEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public ProjectStatus? PreviousStatus { get; set; }
    public ProjectStatus NewStatus { get; set; }

    public string? Description { get; set; }
    public string? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
