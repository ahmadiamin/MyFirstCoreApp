using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Banking;

/// <summary>
/// تاریخچه‌ی تغییرات چک
/// </summary>
public class ChequeHistory : TenantEntity
{
    public int ChequeId { get; set; }
    public Cheque Cheque { get; set; } = null!;

    /// <summary>وضعیت قبلی</summary>
    public ChequeStatus? FromStatus { get; set; }

    /// <summary>وضعیت جدید</summary>
    public ChequeStatus ToStatus { get; set; }

    /// <summary>عملیات</summary>
    public ChequeHistoryAction Action { get; set; }

    /// <summary>توضیحات</summary>
    public string? Description { get; set; }

    /// <summary>تاریخ</summary>
    public DateTime Date { get; set; } = DateTime.UtcNow;

    /// <summary>کاربر</summary>
    public string? UserId { get; set; }
}