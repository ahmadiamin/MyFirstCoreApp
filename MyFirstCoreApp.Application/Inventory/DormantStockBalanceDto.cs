namespace MyFirstCoreApp.Application.Inventory;

public sealed class DormantStockBalanceDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateOnly LastMovementDate { get; set; }
}
