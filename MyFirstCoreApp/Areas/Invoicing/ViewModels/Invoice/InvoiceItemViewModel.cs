using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Invoicing.ViewModels.Invoice;

public class InvoiceItemViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "کالا الزامی است")]
    [Display(Name = "کالا")]
    public int ProductId { get; set; }

    [Display(Name = "انبار")]
    public int? WarehouseId { get; set; }

    /// <summary>null means a product has not been selected yet.</summary>
    public bool? TrackInventory { get; set; }

    // 🆕 برای MagicSuggest
    [Display(Name = "نام کالا")]
    public string? ProductName { get; set; }

    [Display(Name = "شرح")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "تعداد الزامی است")]
    [Display(Name = "تعداد")]
    public decimal Quantity { get; set; } = 1;

    [Required(ErrorMessage = "قیمت الزامی است")]
    [Display(Name = "قیمت واحد")]
    public decimal UnitPrice { get; set; }

    [Display(Name = "درصد تخفیف")]
    public decimal DiscountPercent { get; set; }

    [Display(Name = "مبلغ تخفیف")]
    public decimal DiscountAmount { get; set; }

    [Display(Name = "نرخ مالیات")]
    public decimal TaxRate { get; set; }

    [Display(Name = "مبلغ مالیات")]
    public decimal TaxAmount { get; set; }

    [Display(Name = "جمع خط")]
    public decimal LineTotal { get; set; }

    public string? ProductCode { get; set; }
    public string? UnitName { get; set; }

    // Dropdown
    public List<SelectListItem> ProductOptions { get; set; } = new();
}
