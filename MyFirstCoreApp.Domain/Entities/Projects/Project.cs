using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Contacts;
using MyFirstCoreApp.Domain.Entities.Tenancy;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Projects;

public class Project : TenantEntity
{
    public string Title { get; set; } = string.Empty;

    public string? Code { get; set; }

    // کارفرما؛ در زمان ایجاد پروژه می‌تواند خالی باشد.
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    // مسئول پروژه از اعضای شرکت فعال انتخاب می‌شود.
    public int? ProjectManagerUserTenantId { get; set; }
    public UserTenant? ProjectManager { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public string? Address { get; set; }

    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public string? HoldReason { get; set; }

    public string? CancellationReason { get; set; }

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

    public ICollection<ProjectStage> Stages { get; set; } = new List<ProjectStage>();

    public ICollection<ProjectStatusHistory> StatusHistories { get; set; } = new List<ProjectStatusHistory>();
}
