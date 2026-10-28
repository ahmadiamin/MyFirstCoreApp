using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.StockTransfers;

public sealed class StockTransferViewModel
{
    public string Date { get; set; } = string.Empty;
    public int? SourceWarehouseId { get; set; }
    public int? DestinationWarehouseId { get; set; }
    public string? Description { get; set; }
    public List<StockTransferItemViewModel> Items { get; set; } = [new()];
    public List<SelectListItem> Warehouses { get; set; } = [];
    public List<SelectListItem> Products { get; set; } = [];
}

public sealed class StockTransferItemViewModel
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; } = 1;
}

public sealed class StockTransferListViewModel
{
    public List<StockTransferListItemViewModel> Transfers { get; set; } = [];
}

public sealed class StockTransferListItemViewModel
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string SourceWarehouse { get; set; } = string.Empty;
    public string DestinationWarehouse { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string ItemsSummary { get; set; } = string.Empty;
    public string? Description { get; set; }
}
