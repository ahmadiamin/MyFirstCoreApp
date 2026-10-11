using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Accounting;

/// <summary>
/// گروه‌بندی تراکنش‌ها (حقوق، اجاره، آب و برق، ...)
/// برای گزارش‌گیری و دسته‌بندی
/// </summary>
public class TransactionGroup : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>
    /// برای گروه‌های تو در تو
    /// </summary>
    public int? ParentId { get; set; }
    public TransactionGroup? Parent { get; set; }
    public ICollection<TransactionGroup> Children { get; set; } = new List<TransactionGroup>();
}