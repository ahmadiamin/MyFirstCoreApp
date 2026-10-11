using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>
/// واحد شمارش کالا (عددی، متری، کیلوگرمی، ...)
/// </summary>
public class Unit : TenantEntity
{
    /// <summary>
    /// نام واحد (مثل "عدد"، "متر"، "کیلوگرم")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// نماد واحد (مثل "عدد"، "m"، "kg")
    /// </summary>
    public string? Symbol { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}