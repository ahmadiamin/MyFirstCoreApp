using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MyFirstCoreApp.Areas.Banking.ViewModels.Payment;

public class PaymentAllocationViewModel
{
    public int Id { get; set; }

    [Required]
    public int InvoiceId { get; set; }

    public int InvoiceNumber { get; set; }
    public decimal InvoiceTotal { get; set; }
    public decimal InvoicePaid { get; set; }
    public decimal InvoiceRemaining { get; set; }
    public string InvoiceDate { get; set; } = string.Empty;

    [Required(ErrorMessage = "مبلغ تخصیص الزامی است")]
    public decimal Amount { get; set; }

    public string? Description { get; set; }

    // برای dropdown
    public List<SelectListItem> InvoiceOptions { get; set; } = new();
}