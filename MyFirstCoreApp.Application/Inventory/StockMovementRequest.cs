
namespace MyFirstCoreApp.Application.Inventory;

public class StockMovementRequest
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public int MovementType { get; set; }

    public int? SourceWarehouseId { get; set; }

    public int? DestinationWarehouseId { get; set; }

    public int? ReferenceInvoiceId { get; set; }

    public string? Description { get; set; }

    public List<StockMovementItemRequest> Items { get; set; } = new();
}