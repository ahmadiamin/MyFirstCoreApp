namespace MyFirstCoreApp.Application.Invoicing;

/// <summary>
/// اعتبار آزاد حاصل از یک فاکتور برگشت از خرید
/// </summary>
public class InvoiceAvailableCreditDto
{
    public int CreditInvoiceId { get; set; }

    public int CreditInvoiceNumber { get; set; }

    public DateOnly CreditInvoiceDate { get; set; }

    public decimal CreditAmount { get; set; }

    public decimal AllocatedAmount { get; set; }

    public decimal AvailableAmount => CreditAmount - AllocatedAmount;
}