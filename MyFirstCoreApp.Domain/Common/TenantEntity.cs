namespace MyFirstCoreApp.Domain.Common;

/// <summary>
/// موجودیت‌هایی که به یک Tenant (شرکت/بیزینس) تعلق دارند
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public int TenantId { get; set; }
}