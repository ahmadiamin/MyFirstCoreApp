namespace MyFirstCoreApp.Application.Inventory;

public sealed class InventoryValuationDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? ProductCode { get; init; }
    public string UnitName { get; init; } = string.Empty;
    public int WarehouseId { get; init; }
    public string WarehouseName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal? AverageUnitCost { get; init; }
    public decimal? TotalValue { get; init; }
    public bool HasUnknownCost { get; init; }
}
