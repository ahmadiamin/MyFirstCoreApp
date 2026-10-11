using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>
/// کالا / خدمات
/// </summary>
public class Product : TenantEntity
{
    // ---- اطلاعات پایه ----

    /// <summary>
    /// نام کالا
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// کد کالا (SKU)
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// بارکد
    /// </summary>
    public string? Barcode { get; set; }

    public string? Description { get; set; }

    // ---- دسته‌بندی و واحد ----

    public int? ProductCategoryId { get; set; }
    public ProductCategory? ProductCategory { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    // ---- قیمت ----

    /// <summary>
    /// قیمت خرید آخرین
    /// </summary>
    public decimal LastPurchasePrice { get; set; }

    /// <summary>
    /// قیمت فروش
    /// </summary>
    public decimal SalePrice { get; set; }

    /// <summary>
    /// تاریخ آخرین قیمت
    /// </summary>
    public DateTime? LastPriceUpdatedAt { get; set; }

    // ---- مالیات ----

    /// <summary>
    /// نرخ مالیات (درصد)
    /// </summary>
    public decimal? TaxRate { get; set; }

    // ---- وضعیت ----

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// آیا کالا در فاکتور استفاده شده؟
    /// </summary>
    public bool IsUsedInInvoices { get; set; }

    // ---- Navigation ----

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
}