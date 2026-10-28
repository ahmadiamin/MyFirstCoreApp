using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Application.Inventory;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.StockBalances;

public sealed class StockBalanceReportViewModel
{
    public int? WarehouseId { get; set; }
    public List<SelectListItem> Warehouses { get; set; } = [];
    public IReadOnlyList<StockBalanceDto> Rows { get; set; } = [];
    public bool HasSearched { get; set; }
}

public sealed class DormantStockReportViewModel
{
    public int? WarehouseId { get; set; }
    public int? ProductId { get; set; }
    public string FromDate { get; set; } = string.Empty;
    public string ToDate { get; set; } = string.Empty;
    public bool HasSearched { get; set; }
    public List<SelectListItem> Warehouses { get; set; } = [];
    public List<SelectListItem> Products { get; set; } = [];
    public List<DormantStockReportRowViewModel> Rows { get; set; } = [];
}

public sealed class DormantStockReportRowViewModel
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string LastMovementDate { get; set; } = string.Empty;
    public int DaysWithoutMovement { get; set; }
}
