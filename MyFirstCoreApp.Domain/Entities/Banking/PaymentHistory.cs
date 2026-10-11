using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Banking;

/// <summary>
/// تاریخچه‌ی پرداخت
/// </summary>
public class PaymentHistory : TenantEntity
{
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public PaymentStatus? FromStatus { get; set; }
    public PaymentStatus ToStatus { get; set; }

    public string? Description { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
}