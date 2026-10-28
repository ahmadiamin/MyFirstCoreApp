using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.StockMovementReports;

public sealed class StockMovementReportViewModel
{
    public int? WarehouseId { get; set; }
    public int? ProductId { get; set; }
    public int? MovementType { get; set; }
    public int? Status { get; set; }
    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public bool HasSearched { get; set; }
    public List<SelectListItem> Warehouses { get; set; } = [];
    public List<SelectListItem> Products { get; set; } = [];
    public List<StockMovementReportRowViewModel> Rows { get; set; } = [];
}

public sealed class StockMovementReportRowViewModel
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceWarehouse { get; set; } = string.Empty;
    public string DestinationWarehouse { get; set; } = string.Empty;
    public string ItemsSummary { get; set; } = string.Empty;
    public string? Description { get; set; }
}
