using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Banking;

/// <summary>
/// چک (دریافتنی یا پرداختنی)
/// </summary>
public class Cheque : TenantEntity
{
    // ============================================
    // اطلاعات اصلی
    // ============================================

    /// <summary>شماره چک</summary>
    public string ChequeNumber { get; set; } = string.Empty;

    /// <summary>جهت چک (دریافتنی/پرداختنی)</summary>
    public ChequeDirection Direction { get; set; }

    /// <summary>وضعیت چک</summary>
    public ChequeStatus Status { get; set; } = ChequeStatus.InProgress;

    // ============================================
    // مالی
    // ============================================

    /// <summary>مبلغ چک</summary>
    public decimal Amount { get; set; }

    /// <summary>تاریخ سررسید (فقط تاریخ)</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>تاریخ دریافت/پرداخت (صدور) چک (فقط تاریخ؛ بدون مقدار پیش‌فرض)</summary>
    public DateOnly IssueDate { get; set; }

    /// <summary>تاریخ پاس/نقد شدن (تاریخ انتخابی کاربر؛ فقط تاریخ)</summary>
    public DateOnly? ClearedDate { get; set; }

    /// <summary>تاریخ برگشت (تاریخ انتخابی کاربر؛ فقط تاریخ)</summary>
    public DateOnly? BouncedDate { get; set; }

    // ============================================
    // طرف حساب
    // ============================================

    /// <summary>طرف حساب (مشتری/فروشنده)</summary>
    public int? ContactId { get; set; }
    public Contacts.Contact? Contact { get; set; }

    // ============================================
    // اطلاعات بانکی
    // ============================================

    /// <summary>حساب بانکی مرتبط (دریافتنی: واریز به کدام حساب؛ پرداختنی: از کدام حساب)</summary>
    public int? BankAccountId { get; set; }
    public Accounting.Account? BankAccount { get; set; }

    /// <summary>نام بانک</summary>
    public string? BankName { get; set; }

    /// <summary>شماره حساب</summary>
    public string? AccountNumber { get; set; }

    /// <summary>کد شعبه</summary>
    public string? BranchCode { get; set; }

    /// <summary>نام شعبه</summary>
    public string? BranchName { get; set; }

    // ============================================
    // اطلاعات صاحب چک (برای دریافتنی)
    // ============================================

    /// <summary>نام صاحب چک</summary>
    public string? DrawerName { get; set; }

    /// <summary>کد ملی صاحب چک</summary>
    public string? DrawerNationalId { get; set; }

    // ============================================
    // پیگیری
    // ============================================

    /// <summary>توضیحات</summary>
    public string? Description { get; set; }

    /// <summary>دلیل برگشت</summary>
    public string? BounceReason { get; set; }

    /// <summary>تاریخ پیگیری بعدی (تاریخ انتخابی کاربر؛ فقط تاریخ)</summary>
    public DateOnly? NextFollowUpDate { get; set; }

    /// <summary>مسیر تصویر چک</summary>
    public string? ImagePath { get; set; }

    /// <summary>شماره پیگیری بانک</summary>
    public string? BankTrackingNumber { get; set; }

    // ============================================
    // ارتباط با فاکتور/تراکنش
    // ============================================

    /// <summary>فاکتور مرتبط</summary>
    public int? InvoiceId { get; set; }
    public Invoicing.Invoice? Invoice { get; set; }

    /// <summary>
    /// سند «پاس/نقد شدن» (بانک ↔ حساب چک). فقط وقتی چک پاس یا نقد شده مقدار دارد.
    /// </summary>
    public int? TransactionId { get; set; }
    public Accounting.Transaction? Transaction { get; set; }

    /// <summary>
    /// سند «ثبت چک» (حساب چک ↔ طرف حساب) در دوره‌ی جاری «در جریان».
    /// هنگام ثبت چک (و ارائه‌ی مجدد) ساخته می‌شود و هنگام برگشت/ابطال با سند معکوس بسته می‌شود
    /// (آنگاه null است). مانده‌ی طرف حساب از همین سند بلافاصله اصلاح می‌شود.
    /// </summary>
    public int? RegistrationTransactionId { get; set; }

    // ============================================
    // Navigation
    // ============================================

    /// <summary>تاریخچه‌ی تغییرات</summary>
    public ICollection<ChequeHistory> Histories { get; set; } = new List<ChequeHistory>();
}
