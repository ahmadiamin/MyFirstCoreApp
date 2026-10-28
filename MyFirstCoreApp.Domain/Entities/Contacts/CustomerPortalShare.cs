using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Contacts;

/// <summary>لینک دسترسی فقط‌خواندنی مشتری به اطلاعات مالی منتخب خودش.</summary>
public sealed class CustomerPortalShare : TenantEntity
{
    public int ContactId { get; set; }
    public Contact Contact { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public string? ProtectedToken { get; set; }
    public string? LastSmsRequestId { get; set; }
    public DateTime? LastSmsSentAtUtc { get; set; }
    public bool CanViewLedger { get; set; }
    public bool CanViewSalesInvoices { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
