namespace MyFirstCoreApp.Application.Inventory;

public sealed class InventoryAlertDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string? ProductCode { get; init; }
    public string UnitName { get; init; } = string.Empty;
    public decimal MinimumStock { get; init; }
    public decimal CurrentStock { get; init; }
    public decimal Shortage { get; init; }
}
