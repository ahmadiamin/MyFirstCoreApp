using System.ComponentModel.DataAnnotations;

namespace MyFirstCoreApp.Areas.Invoicing.ViewModels.InvoiceReturn;

public sealed class InvoiceReturnViewModel
{
    public int OriginalInvoiceId { get; set; }

    public int OriginalInvoiceNumber { get; set; }

    public string ContactName { get; set; } = string.Empty;

    public string ReturnTypeName { get; set; } = string.Empty;

    [Required(ErrorMessage = "تاریخ برگشت الزامی است.")]
    [Display(Name = "تاریخ برگشت")]
    public string Date { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Display(Name = "شرح")]
    public string? Description { get; set; }

    public List<InvoiceReturnItemViewModel> Items { get; set; } = new();
}

public sealed class InvoiceReturnItemViewModel
{
    public int OriginalInvoiceItemId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal OriginalQuantity { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    [Range(typeof(decimal), "0", "999999999999",
        ErrorMessage = "مقدار برگشت معتبر نیست")]
    [Display(Name = "مقدار برگشتی")]
    public decimal ReturnQuantity { get; set; }
}