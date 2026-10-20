using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Invoicing.ViewModels.InvoicePoem;

public class InvoicePoemViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "متن شعر الزامی است.")]
    [StringLength(2000, ErrorMessage = "متن شعر نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد.")]
    [Display(Name = "متن شعر")]
    public string Text { get; set; } = string.Empty;

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;
}