using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Banking;

/// <summary>
/// ورودی ثبت/ویرایش چک. فقط فیلدهایی که کاربر اجازه‌ی تعیین‌شان را دارد
/// (وضعیت، تاریخ پاس/برگشت، سندها و ... هرگز از ورودی گرفته نمی‌شوند).
/// </summary>
public sealed class ChequeRequest
{
    public string ChequeNumber { get; set; } = string.Empty;
    public ChequeDirection Direction { get; set; }
    public decimal Amount { get; set; }

    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }

    public int? ContactId { get; set; }
    public int? BankAccountId { get; set; }

    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }

    public string? DrawerName { get; set; }
    public string? DrawerNationalId { get; set; }

    public string? Description { get; set; }
    public int? InvoiceId { get; set; }
    public string? ImagePath { get; set; }
}

/// <summary>قواعد وضعیت چک؛ هم سرویس و هم کنترلر از همین‌جا می‌خوانند.</summary>
public static class ChequeRules
{
    /// <summary>
    /// «در انتظار»: چک هنوز در دست وصول است و در دفتر با سند ثبت چک ثبت‌شده است.
    /// فقط از همین وضعیت‌ها می‌شود چک را پاس، نقد یا برگشت زد.
    /// </summary>
    public static bool IsPending(ChequeStatus status)
        => status is ChequeStatus.InProgress or ChequeStatus.RePresented;

    /// <summary>
    /// چک در این وضعیت‌ها می‌تواند «پشتوانه‌ی یک پرداخت» باشد (هنوز وصول‌شدنی است یا وصول شده).
    /// چک برگشتی، عودتی، ارجاعی و ابطال‌شده نمی‌تواند.
    /// </summary>
    public static bool CanBePaymentSource(ChequeStatus status)
        => IsPending(status) || status is ChequeStatus.Cleared or ChequeStatus.Cashed;

    /// <summary>وضعیت‌هایی که ویرایش اطلاعات چک مجاز است (پاس/نقد/ابطال‌شده مجاز نیست).</summary>
    public static bool IsEditable(ChequeStatus status)
        => status is ChequeStatus.InProgress
            or ChequeStatus.RePresented
            or ChequeStatus.Returned
            or ChequeStatus.Referred;
}
