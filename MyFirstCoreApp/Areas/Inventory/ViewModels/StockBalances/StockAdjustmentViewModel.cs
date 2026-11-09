using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.StockBalances;

public sealed class StockAdjustmentViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string UnitName { get; set; } = string.Empty;

    [Required(ErrorMessage = "انتخاب انبار الزامی است.")]
    [Display(Name = "انبار")]
    public int WarehouseId { get; set; }

    [Display(Name = "موجودی فعلی سیستم")]
    public decimal CurrentQuantity { get; set; }

    [Required(ErrorMessage = "موجودی واقعی را وارد کنید.")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "موجودی واقعی نمی‌تواند منفی باشد.")]
    [Display(Name = "موجودی واقعی جدید")]
    public decimal TargetQuantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "بهای واحد نمی‌تواند منفی باشد.")]
    [Display(Name = "بهای تمام‌شده واحد (اختیاری)")]
    public decimal? UnitCost { get; set; }

    public List<SelectListItem> Warehouses { get; set; } = [];
}
