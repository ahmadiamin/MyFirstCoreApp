using MyFirstCoreApp.Domain.Common;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Domain.Entities.Inventory;

/// <summary>
/// سربرگ گردش کالا
/// </summary>
public class StockMovement : TenantEntity
{
    /// <summary>
    /// شماره سند گردش کالا
    /// </summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// تاریخ سند
    /// </summary>
    public DateOnly Date { get; set; }

    public StockMovementType MovementType { get; set; }

    public StockMovementStatus Status { get; set; }
        = StockMovementStatus.Draft;

    /// <summary>
    /// انبار مبدأ؛ برای رسید و افتتاحیه خالی است
    /// </summary>
    public int? SourceWarehouseId { get; set; }

    public Warehouse? SourceWarehouse { get; set; }

    /// <summary>
    /// انبار مقصد؛ برای حواله خروج خالی است
    /// </summary>
    public int? DestinationWarehouseId { get; set; }

    public Warehouse? DestinationWarehouse { get; set; }

    /// <summary>
    /// فاکتور مرتبط، در صورت ایجاد گردش از روی فاکتور
    /// </summary>
    public int? ReferenceInvoiceId { get; set; }

    public Invoice? ReferenceInvoice { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// زمان قطعی‌شدن سند
    /// </summary>
    public DateTime? PostedAt { get; set; }

    public ICollection<StockMovementItem> Items { get; set; }
        = new List<StockMovementItem>();

    /// <summary>
    /// شناسه گردش اصلی که این سند، اثر آن را معکوس می‌کند.
    /// برای گردش‌های عادی مقدار null دارد.
    /// </summary>
    public int? ReversesMovementId { get; set; }

    public StockMovement? ReversesMovement { get; set; }

    public ICollection<StockMovement> ReversalMovements { get; set; }
        = new List<StockMovement>();
}