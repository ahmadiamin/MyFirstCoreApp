using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Tenancy;

namespace MyFirstCoreApp.Domain.Entities.Projects;

public class ProjectMember : TenantEntity
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int UserTenantId { get; set; }
    public UserTenant UserTenant { get; set; } = null!;

    // مثلاً نصاب، سرپرست اجرا، کارشناس فنی
    public string? RoleTitle { get; set; }

    public string? Notes { get; set; }
}