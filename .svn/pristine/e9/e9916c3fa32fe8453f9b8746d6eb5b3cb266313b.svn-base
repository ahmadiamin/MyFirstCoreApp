using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Accounting;

/// <summary>
/// دسته‌بندی حساب‌ها (بانک‌ها، قبوض، نقد، درآمدها، هزینه‌ها، ...)
/// </summary>
public class AccountCategory : TenantEntity
{
    /// <summary>
    /// نام دسته (مثل "بانک‌ها"، "قبوض")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// کد دسته (مثلاً 1001)
    /// </summary>
    public string? Code { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// برای ساخت درخت دسته‌بندی
    /// </summary>
    public int? ParentId { get; set; }
    public AccountCategory? Parent { get; set; }
    public ICollection<AccountCategory> Children { get; set; } = new List<AccountCategory>();

    /// <summary>
    /// نوع پیش‌فرض حساب‌های این دسته
    /// </summary>
    public AccountType DefaultAccountType { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Navigation
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}