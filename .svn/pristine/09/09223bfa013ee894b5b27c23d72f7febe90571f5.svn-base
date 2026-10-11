using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Invoicing;

/// <summary>
/// تاریخچه‌ی تغییرات وضعیت فاکتور
/// </summary>
public class InvoiceHistory : TenantEntity
{
    /// <summary>
    /// فاکتور مرتبط
    /// </summary>
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    /// <summary>
    /// وضعیت قبلی
    /// </summary>
    public Domain.Enums.InvoiceStatus? FromStatus { get; set; }

    /// <summary>
    /// وضعیت جدید
    /// </summary>
    public Domain.Enums.InvoiceStatus ToStatus { get; set; }

    /// <summary>
    /// توضیحات (دلیل رد، یادداشت، ...)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// تاریخ
    /// </summary>
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// کاربر (اختیاری)
    /// </summary>
    public string? UserId { get; set; }
}