using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>
/// تصاویر کالا
/// </summary>
public class ProductImage : TenantEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// مسیر فایل
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// آیا تصویر اصلی است؟
    /// </summary>
    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }

    public string? Title { get; set; }
}