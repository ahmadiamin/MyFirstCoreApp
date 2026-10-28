using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.Product;

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام کالا الزامی است")]
    [Display(Name = "نام کالا")]
    [MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "کد کالا")]
    [MaxLength(50)]
    public string? Code { get; set; }

    [Display(Name = "بارکد")]
    [MaxLength(50)]
    public string? Barcode { get; set; }

    [Required(ErrorMessage = "واحد الزامی است")]
    [Display(Name = "واحد")]
    public int UnitId { get; set; }

    [Display(Name = "دسته‌بندی")]
    public int? ProductCategoryId { get; set; }

    [Display(Name = "قیمت خرید")]
    public decimal LastPurchasePrice { get; set; }

    [Required(ErrorMessage = "قیمت فروش الزامی است")]
    [Display(Name = "قیمت فروش")]
    public decimal SalePrice { get; set; }

    [Display(Name = "حداقل موجودی")]
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "حداقل موجودی نمی‌تواند منفی باشد")]
    public decimal? MinimumStock { get; set; }

    [Display(Name = "نرخ مالیات (درصد)")]
    public decimal? TaxRate { get; set; }

    [Display(Name = "توضیحات")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "کنترل موجودی")]
    public bool TrackInventory { get; set; } = true;

    // Dropdowns
    public List<SelectListItem> UnitOptions { get; set; } = new();
    public List<SelectListItem> CategoryOptions { get; set; } = new();
}
