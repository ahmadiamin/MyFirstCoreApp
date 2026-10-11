using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.Unit;

public class UnitFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "نام واحد الزامی است")]
    [Display(Name = "نام واحد")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "نماد")]
    [MaxLength(20)]
    public string? Symbol { get; set; }

    [Display(Name = "ترتیب")]
    public int SortOrder { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}