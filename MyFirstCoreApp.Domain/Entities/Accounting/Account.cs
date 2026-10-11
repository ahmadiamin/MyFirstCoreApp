using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Accounting;

/// <summary>
/// حساب‌های مالی (بانک صادرات، صندوق نقدی، سرمایه، ...)
/// </summary>
public class Account : TenantEntity
{
    /// <summary>
    /// نام حساب (مثل "بانک صادرات")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// کد حساب
    /// </summary>
    public string? Code { get; set; }

    public string? Description { get; set; }

    public int AccountCategoryId { get; set; }
    public AccountCategory AccountCategory { get; set; } = null!;

    /// <summary>
    /// نوع حساب (بانک، نقد، ...)
    /// </summary>
    public AccountType AccountType { get; set; }

    // ---- اطلاعات بانکی (فقط برای AccountType.Bank) ----

    /// <summary>نام بانک</summary>
    public string? BankName { get; set; }

    /// <summary>شماره حساب</summary>
    public string? AccountNumber { get; set; }

    /// <summary>شماره کارت</summary>
    public string? CardNumber { get; set; }

    /// <summary>شماره شبا</summary>
    public string? ShebaNumber { get; set; }

    // ---- مالی ----

    /// <summary>
    /// مانده حساب (برای نمایش سریع - محاسبه از تراکنش‌ها)
    /// </summary>
    public decimal CurrentBalance { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// آیا حساب پیش‌فرض این نوع است؟
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// برای حساب‌های زیرمجموعه (مثل معین/تفصیلی)
    /// </summary>
    public int? ParentId { get; set; }
    public Account? Parent { get; set; }
    public ICollection<Account> Children { get; set; } = new List<Account>();
}