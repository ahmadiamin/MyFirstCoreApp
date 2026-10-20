using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Invoicing.ViewModels.InvoicePrintSetting;

public class InvoicePrintSettingViewModel
{
    [Display(Name = "نمایش مانده حساب")]
    public bool ShowBalance { get; set; } = true;

    [Display(Name = "روش انتخاب شعر")]
    public InvoicePoemSelectionMode PoemSelectionMode { get; set; }
        = InvoicePoemSelectionMode.Random;

    [Display(Name = "شعر انتخاب‌شده")]
    public int? SelectedInvoicePoemId { get; set; }

    [StringLength(2000, ErrorMessage = "متن توضیحات فاکتور فروش نمی‌تواند بیشتر از ۲۰۰۰ کاراکتر باشد.")]
    [Display(Name = "توضیحات فاکتور فروش")]
    public string Description { get; set; } = string.Empty;

    public List<SelectListItem> Poems { get; set; } = new();
}