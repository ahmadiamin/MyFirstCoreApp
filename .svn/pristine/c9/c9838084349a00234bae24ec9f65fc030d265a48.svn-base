using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Contacts;

/// <summary>
/// طرف حساب (مشتری، فروشنده، همکار، ...)
/// </summary>
public class Contact : TenantEntity
{
    // ---- اطلاعات پایه ----

    /// <summary>
    /// نام کامل (شخص یا شرکت)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// کد طرف حساب
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// نوع طرف حساب (مشتری/فروشنده/هر دو)
    /// </summary>
    public ContactType ContactType { get; set; } = ContactType.Customer;

    // ---- اطلاعات تماس ----

    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }

    // ---- اطلاعات حقوقی ----

    /// <summary>شناسه ملی / کد ملی</summary>
    public string? NationalId { get; set; }

    /// <summary>کد اقتصادی</summary>
    public string? EconomicCode { get; set; }

    /// <summary>شماره ثبت</summary>
    public string? RegistrationNumber { get; set; }

    // ---- مالی ----

    /// <summary>
    /// مانده حساب (مثبت = طلبکار ما، منفی = بدهکار ما)
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// سقف اعتبار
    /// </summary>
    public decimal? CreditLimit { get; set; }

    // ---- وضعیت ----

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}