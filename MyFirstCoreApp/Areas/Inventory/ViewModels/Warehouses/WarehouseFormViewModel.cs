using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Inventory.ViewModels.Warehouse;

public class WarehouseFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "کد انبار الزامی است.")]
    [StringLength(50, ErrorMessage = "کد انبار نمی‌تواند بیش از ۵۰ کاراکتر باشد.")]
    [Display(Name = "کد انبار")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام انبار الزامی است.")]
    [StringLength(200, ErrorMessage = "نام انبار نمی‌تواند بیش از ۲۰۰ کاراکتر باشد.")]
    [Display(Name = "نام انبار")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "آدرس نمی‌تواند بیش از ۵۰۰ کاراکتر باشد.")]
    [Display(Name = "آدرس")]
    public string? Address { get; set; }

    [StringLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیش از ۱۰۰۰ کاراکتر باشد.")]
    [Display(Name = "توضیحات")]
    public string? Description { get; set; }

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "انبار پیش‌فرض")]
    public bool IsDefault { get; set; }
}
