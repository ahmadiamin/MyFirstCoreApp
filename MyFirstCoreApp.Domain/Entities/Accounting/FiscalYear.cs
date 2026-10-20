using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Accounting;

/// <summary>
/// سال مالی یک شرکت. هر سند حسابداری دقیقاً در یک سال مالی قرار می‌گیرد
/// (سالی که تاریخ سند در بازه‌ی آن است).
///
/// قواعد:
///  - سال‌های مالی یک شرکت همپوشانی ندارند.
///  - بازه‌ها شامل دو سر خود هستند: StartDate ≤ تاریخ سند ≤ EndDate.
///  - در سال بسته هیچ سندی ساخته، ویرایش یا حذف نمی‌شود.
/// </summary>
public class FiscalYear : TenantEntity
{
    /// <summary>نام نمایشی، مثلاً «سال مالی 1405».</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>اولین روز سال مالی (میلادی؛ فقط تاریخ).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>آخرین روز سال مالی (میلادی؛ فقط تاریخ).</summary>
    public DateOnly EndDate { get; set; }

    public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;

    /// <summary>لحظه‌ی بسته شدن (UTC) و کاربری که آن را بسته است.</summary>
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
}
