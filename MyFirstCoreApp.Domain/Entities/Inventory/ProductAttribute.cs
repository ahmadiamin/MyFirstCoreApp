using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>
/// مشخصات کالا (رنگ، سایز، وزن، ...)
/// </summary>
public class ProductAttribute : TenantEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// نام ویژگی (مثل "رنگ")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// مقدار ویژگی (مثل "قرمز")
    /// </summary>
    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}