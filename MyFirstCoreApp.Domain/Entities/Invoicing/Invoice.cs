using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Invoicing;

/// <summary>
/// فاکتور (فروش، خرید، پیش‌فاکتور)
/// </summary>
public class Invoice : TenantEntity
{
    /// <summary>
    /// شماره فاکتور (خودکار)
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// تاریخ فاکتور
    /// </summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// نوع فاکتور (فروش، خرید، پیش‌فاکتور)
    /// </summary>
    public InvoiceType InvoiceType { get; set; }

    /// <summary>
    /// طرف حساب (مشتری/فروشنده)
    /// </summary>
    public int? ContactId { get; set; }
    public Contacts.Contact? Contact { get; set; }

    /// <summary>
    /// وضعیت (پیش‌نویس، قطعی، لغو شده)
    /// </summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>
    /// شرح
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// شماره فاکتور مرجع (برای پیش‌فاکتور که تبدیل شده)
    /// </summary>
    public int? ReferenceInvoiceId { get; set; }
    public Invoice? ReferenceInvoice { get; set; }

    // ---- مالی ----

    /// <summary>
    /// جمع اقلام (قبل از تخفیف و مالیات)
    /// </summary>
    public decimal SubTotal { get; set; }

    /// <summary>
    /// جمع تخفیف
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// جمع مالیات
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// مبلغ نهایی (SubTotal - Discount + Tax)
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// مبلغ پرداخت‌شده
    /// </summary>
    public decimal PaidAmount { get; set; }

    //// <summary>
    /// مبلغ باقی‌مانده (بعد از پرداخت و تخفیف تسویه)
    /// </summary>
    public decimal RemainingAmount => TotalAmount - PaidAmount - SettlementDiscount;

    /// <summary>
    /// سند حسابداری مرتبط
    /// </summary>
    public int? TransactionId { get; set; }
    public Accounting.Transaction? Transaction { get; set; }

    // ---- Navigation ----

    // ============================================
    // 🆕 گردش کار پیش‌فاکتور
    // ============================================

    /// <summary>
    /// تاریخ ارسال به مشتری
    /// </summary>
    public DateTime? SentDate { get; set; }

    /// <summary>
    /// تاریخ آخرین ارسال (آخرین بار که به مشتری داده شد)
    /// </summary>
    public DateTime? LastSentDate { get; set; }

    /// <summary>
    /// تاریخ تایید مشتری
    /// </summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// تاریخ رد مشتری
    /// </summary>
    public DateTime? RejectedDate { get; set; }

    /// <summary>
    /// تاریخ پیگیری بعدی (اگه به تعویق افتاد)
    /// </summary>
    public DateTime? NextFollowUpDate { get; set; }

    /// <summary>
    /// پاسخ/توضیحات مشتری
    /// </summary>
    public string? CustomerResponse { get; set; }

    /// <summary>
    /// تعداد دفعات اصلاح
    /// </summary>
    public int RevisionCount { get; set; }

    /// <summary>
    /// مبلغ تخفیف تسویه (تخفیف نهایی برای بستن فاکتور)
    /// </summary>
    public decimal SettlementDiscount { get; set; }

    // ============================================
    // 🆕 Navigation
    // ============================================
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();

    /// <summary>
    /// تاریخچه‌ی تغییرات
    /// </summary>
    public ICollection<InvoiceHistory> Histories { get; set; } = new List<InvoiceHistory>();

}