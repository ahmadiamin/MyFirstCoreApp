using MyFirstCoreApp.Domain.Common;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>
/// ردیف کالا در سند گردش انبار
/// </summary>
public class StockMovementItem : TenantEntity
{
    public int StockMovementId { get; set; }

    public StockMovement StockMovement { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>
    /// تعداد یا مقدار کالا
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// بهای واحد؛ در صورت نیاز به محاسبه بهای موجودی
    /// </summary>
    public decimal? UnitCost { get; set; }

    public string? Description { get; set; }
}