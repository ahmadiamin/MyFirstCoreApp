using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Accounting;

/// <summary>
/// خط تراکنش (بدهکار یا بستانکار)
/// </summary>
public class TransactionLine : TenantEntity
{
    /// <summary>
    /// شماره خط (ترتیب نمایش)
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// ارجاع به هدر تراکنش
    /// </summary>
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    /// <summary>
    /// حساب (بانک، هزینه، درآمد، ...)
    /// </summary>
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    /// <summary>
    /// طرف حساب (اختیاری - برای فاکتورها)
    /// </summary>
    public int? ContactId { get; set; }
    public Contacts.Contact? Contact { get; set; }

    /// <summary>
    /// مبلغ بدهکار (اگه صفر باشه، یعنی بستانکار)
    /// </summary>
    public decimal Debit { get; set; }

    /// <summary>
    /// مبلغ بستانکار (اگه صفر باشه، یعنی بدهکار)
    /// </summary>
    public decimal Credit { get; set; }

    /// <summary>
    /// شرح خط
    /// </summary>
    public string? Description { get; set; }
} 