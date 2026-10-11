using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Transaction;

public class TransactionFormViewModel
{
    public int Id { get; set; }
    [Display(Name = "شماره سند")]
    public int Number { get; set; }

    [Required(ErrorMessage = "تاریخ الزامی است")]
    [Display(Name = "تاریخ")]
    public string Date { get; set; } = string.Empty;

    [Display(Name = "شرح")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Display(Name = "گروه تراکنش")]
    public int? TransactionGroupId { get; set; }

    [Display(Name = "مرجع")]
    [MaxLength(200)]
    public string? Reference { get; set; }

    [Display(Name = "پیوست")]
    public IFormFile? Attachment { get; set; }
    public string? AttachmentPath { get; set; }

    public List<TransactionLineViewModel> Lines { get; set; } = new();

    public List<SelectListItem> AccountOptions { get; set; } = new();
    public List<SelectListItem> ContactOptions { get; set; } = new();
    public List<SelectListItem> TransactionGroupOptions { get; set; } = new();
}