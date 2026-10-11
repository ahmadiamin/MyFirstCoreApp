using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Invoicing;

/// <summary>
/// خط فاکتور (کالا/خدمات)
/// </summary>
public class InvoiceItem : TenantEntity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    /// <summary>
    /// کالا
    /// </summary>
    public int ProductId { get; set; }
    public Inventory.Product Product { get; set; } = null!;

    /// <summary>
    /// شرح (اختیاری)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// تعداد (می‌تونه اعشار داشته باشه - کیلو، متر)
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// قیمت واحد
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// درصد تخفیف
    /// </summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>
    /// مبلغ تخفیف
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// نرخ مالیات (درصد)
    /// </summary>
    public decimal TaxRate { get; set; }

    /// <summary>
    /// مبلغ مالیات
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// جمع خط (Quantity * UnitPrice - Discount + Tax)
    /// </summary>
    public decimal LineTotal { get; set; }
}