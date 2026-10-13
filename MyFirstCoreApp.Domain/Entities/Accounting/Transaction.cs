using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Accounting;

/// <summary>
/// هدر تراکنش (سند حسابداری)
/// </summary>
public class Transaction : TenantEntity
{
    /// <summary>
    /// شماره سند (خودکار تولید می‌شه)
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// تاریخ سند (فقط تاریخ، بدون ساعت و منطقه‌ی زمانی).
    /// مقدار پیش‌فرض ندارد؛ سازنده‌ی سند باید صراحتاً تعیین کند
    /// (مثلاً با TimeProvider.Today() یا تاریخ واردشده‌ی کاربر).
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// شرح کلی سند
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// گروه تراکنش (حقوق، اجاره، خرید، ...)
    /// </summary>
    public int? TransactionGroupId { get; set; }
    public TransactionGroup? TransactionGroup { get; set; }

    /// <summary>
    /// شماره مرجع (شماره فاکتور، شماره چک، ...)
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// مسیر فایل پیوست (رسید، تصویر چک، ...)
    /// </summary>
    public string? AttachmentPath { get; set; }

    /// <summary>
    /// نوع تراکنش (عادی، افتتاحیه، اختتامیه، ...)
    /// </summary>
    public int TransactionType { get; set; } = 1;

    /// <summary>
    /// آیا سند قطعی شده؟ (بعد از قطعی، قابل ویرایش نیست)
    /// </summary>
    public bool IsPosted { get; set; } = false;

    /// <summary>
    /// مجموع مبلغ سند (برای نمایش سریع)
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// نوع مرجع (اگه سند خودکار باشه)
    /// مقادیر: "Invoice", "Cheque", "Payment", null (دستی)
    /// </summary>
    public string? ReferenceType { get; set; }

    /// <summary>
    /// شناسه‌ی مرجع (مثلاً InvoiceId، ChequeId، PaymentId)
    /// </summary>
    public int? ReferenceId { get; set; }

    // Navigation
    public ICollection<TransactionLine> Lines { get; set; } = new List<TransactionLine>();
}