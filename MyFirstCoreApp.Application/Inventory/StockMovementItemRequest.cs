
namespace MyFirstCoreApp.Application.Inventory;

public class StockMovementItemRequest
{
    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public string? Description { get; set; }
}
