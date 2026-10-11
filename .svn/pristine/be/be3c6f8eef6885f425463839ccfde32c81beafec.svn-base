using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Banking;

/// <summary>
/// تخصیص پرداخت به فاکتور
/// </summary>
public class PaymentAllocation : TenantEntity
{
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    /// <summary>فاکتور مرتبط</summary>
    public int InvoiceId { get; set; }
    public Invoicing.Invoice Invoice { get; set; } = null!;

    /// <summary>مبلغ تخصیص‌یافته</summary>
    public decimal Amount { get; set; }

    /// <summary>توضیحات</summary>
    public string? Description { get; set; }
}