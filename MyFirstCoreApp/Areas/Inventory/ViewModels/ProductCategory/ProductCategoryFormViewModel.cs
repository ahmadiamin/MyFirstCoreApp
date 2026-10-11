using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.ProductCategory;

public class ProductCategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام الزامی است")]
    [Display(Name = "نام")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "کد")]
    [MaxLength(50)]
    public string? Code { get; set; }

    [Display(Name = "توضیحات")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "ترتیب")]
    public int SortOrder { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}