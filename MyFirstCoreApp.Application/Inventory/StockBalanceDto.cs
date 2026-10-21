namespace MyFirstCoreApp.Application.Inventory;

/// <summary>
/// موجودی یک کالا در یک انبار
/// </summary>
public class StockBalanceDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? ProductCode { get; set; }

    public int WarehouseId { get; set; }

    public string WarehouseName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
}