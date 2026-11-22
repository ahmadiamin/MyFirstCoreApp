using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>An individually tracked company tool or fixed asset.</summary>
public class CompanyAsset : TenantEntity
{
    public int Number { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public string? Description { get; set; }
    public CompanyAssetStatus Status { get; set; } = CompanyAssetStatus.Available;
    public ICollection<CompanyAssetAssignment> Assignments { get; set; } = new List<CompanyAssetAssignment>();
}
