using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Accounting.ViewModels.Transaction;

public class TransactionLineViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "حساب الزامی است")]
    [Display(Name = "حساب")]
    public int AccountId { get; set; }

    [Display(Name = "طرف حساب")]
    public int? ContactId { get; set; }

    [Display(Name = "بدهکار")]
    public decimal? Debit { get; set; }

    [Display(Name = "بستانکار")]
    public decimal? Credit { get; set; }

    [Display(Name = "شرح")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    public List<SelectListItem> AccountOptions { get; set; } = new();
    public List<SelectListItem> ContactOptions { get; set; } = new();
}