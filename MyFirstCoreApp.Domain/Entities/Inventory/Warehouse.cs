using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>
/// انبار متعلق به یک شرکت
/// </summary>
public class Warehouse : TenantEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public ICollection<StockMovement> SourceMovements { get; set; }
        = new List<StockMovement>();

    public ICollection<StockMovement> DestinationMovements { get; set; }
        = new List<StockMovement>();
}