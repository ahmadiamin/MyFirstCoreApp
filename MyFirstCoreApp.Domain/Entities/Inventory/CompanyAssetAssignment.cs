using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Projects;
using MyFirstCoreApp.Domain.Entities.Tenancy;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>One handover period; a returned handover remains as history.</summary>
public class CompanyAssetAssignment : TenantEntity
{
    public int CompanyAssetId { get; set; }
    public CompanyAsset CompanyAsset { get; set; } = null!;
    public int? UserTenantId { get; set; }
    public UserTenant? UserTenant { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public DateOnly AssignedDate { get; set; }
    public string? AssignedByUserId { get; set; }
    public DateOnly? ReturnedDate { get; set; }
    public string? ReturnedByUserId { get; set; }
    public string? Notes { get; set; }
    public string? ReturnNotes { get; set; }
}
