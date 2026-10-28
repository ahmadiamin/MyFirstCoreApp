using MyFirstCoreApp.Application.Inventory;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IStockBalanceService
{
    /// <summary>
    /// موجودی یک کالا در یک انبار
    /// </summary>
    Task<decimal> GetBalanceAsync(
        int productId,
        int warehouseId,
        int tenantId);

    /// <summary>
    /// موجودی کالاهای دارای گردش در یک انبار
    /// </summary>
    Task<IReadOnlyList<StockBalanceDto>> GetWarehouseBalancesAsync(
        int warehouseId,
        int tenantId);

    /// <summary>
    /// موجودی یک کالا در تمام انبارهای شرکت
    /// </summary>
    Task<IReadOnlyList<StockBalanceDto>> GetProductBalancesAsync(
        int productId,
        int tenantId);

    /// <summary>
    /// موجودی تمام کالاها به تفکیک انبار، با امکان محدودکردن به یک انبار
    /// </summary>
    Task<IReadOnlyList<StockBalanceDto>> GetAllBalancesAsync(
        int tenantId,
        int? warehouseId = null);

    Task<IReadOnlyList<DormantStockBalanceDto>> GetDormantBalancesAsync(
        int tenantId,
        DateOnly from,
        DateOnly to,
        int? warehouseId = null,
        int? productId = null);
}
