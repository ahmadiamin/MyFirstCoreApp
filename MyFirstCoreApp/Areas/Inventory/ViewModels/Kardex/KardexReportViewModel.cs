using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Application.Inventory;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.Kardex;

public class KardexReportViewModel
{
    public int? ProductId { get; set; }

    public int? WarehouseId { get; set; }

    public string FromDate { get; set; } = string.Empty;

    public string ToDate { get; set; } = string.Empty;

    public string? ProductName { get; set; }

    public string? ProductCode { get; set; }

    public string? UnitName { get; set; }

    public string? WarehouseName { get; set; }

    public List<SelectListItem> ProductOptions { get; set; } = new();

    public List<SelectListItem> WarehouseOptions { get; set; } = new();

    public List<KardexRowDto> Rows { get; set; } = new();
}