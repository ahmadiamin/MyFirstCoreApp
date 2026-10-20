using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Banking;

/// <summary>
/// پرداخت/دریافت
/// </summary>
public class Payment : TenantEntity
{
    // ============================================
    // اطلاعات اصلی
    // ============================================

    /// <summary>شماره پرداخت (خودکار)</summary>
    public int Number { get; set; }

    /// <summary>نوع (دریافت/پرداخت)</summary>
    public PaymentType PaymentType { get; set; }

    /// <summary>وضعیت</summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.Draft;

    /// <summary>
    /// تاریخ (فقط تاریخ). مقدار پیش‌فرض ندارد؛ سازنده باید صراحتاً تعیین کند.
    /// (پیش‌فرض قبلی DateTime.Now به ساعت سرور میزبان وابسته بود.)
    /// </summary>
    public DateOnly Date { get; set; }

    // ============================================
    // طرف حساب
    // ============================================

    public int? ContactId { get; set; }
    public Contacts.Contact? Contact { get; set; }

    // ============================================
    // حساب بانکی
    // ============================================

    /// <summary>حساب بانکی (مبدأ پرداخت یا مقصد دریافت)</summary>
    public int? BankAccountId { get; set; }
    public Accounting.Account? BankAccount { get; set; }

    // ============================================
    // مالی
    // ============================================

    /// <summary>روش پرداخت</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;

    /// <summary>مبلغ کل پرداخت</summary>
    public decimal Amount { get; set; }

    /// <summary>مبلغ تخصیص‌یافته</summary>
    public decimal AllocatedAmount { get; set; }

    /// <summary>مبلغ تخصیص‌نیافته (اضافه پرداخت)</summary>
    public decimal UnallocatedAmount => Amount - AllocatedAmount;

    // ============================================
    // اطلاعات مرجع
    // ============================================

    /// <summary>شماره مرجع (شماره رسید، پیگیری، ...)</summary>
    public string? Reference { get; set; }

    /// <summary>توضیحات</summary>
    public string? Description { get; set; }

    /// <summary>مسیر تصویر رسید</summary>
    public string? ImagePath { get; set; }

    // ============================================
    // ارتباط
    // ============================================

    /// <summary>چک مرتبط (اگه با چک پرداخت شده)</summary>
    public int? ChequeId { get; set; }
    public Cheque? Cheque { get; set; }

    /// <summary>سند حسابداری</summary>
    public int? TransactionId { get; set; }
    public Accounting.Transaction? Transaction { get; set; }

    // ============================================
    // Navigation
    // ============================================

    /// <summary>تخصیص‌ها به فاکتورها</summary>
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();

    /// <summary>تاریخچه</summary>
    public ICollection<PaymentHistory> Histories { get; set; } = new List<PaymentHistory>();
}
