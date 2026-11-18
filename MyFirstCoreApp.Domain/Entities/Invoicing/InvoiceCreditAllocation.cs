using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Invoicing;

/// <summary>
/// تخصیص اعتبار حاصل از برگشت خرید به فاکتور خرید
/// </summary>
public class InvoiceCreditAllocation : TenantEntity
{
    /// <summary>
    /// فاکتور برگشت خرید (منبع اعتبار)
    /// </summary>
    public int CreditInvoiceId { get; set; }
    public Invoice CreditInvoice { get; set; } = null!;

    /// <summary>
    /// فاکتور خریدی که اعتبار به آن تخصیص یافته است
    /// </summary>
    public int TargetInvoiceId { get; set; }
    public Invoice TargetInvoice { get; set; } = null!;

    /// <summary>
    /// مبلغ اعتبار تخصیص‌یافته
    /// </summary>
    public decimal Amount { get; set; }

    public string? Description { get; set; }
}