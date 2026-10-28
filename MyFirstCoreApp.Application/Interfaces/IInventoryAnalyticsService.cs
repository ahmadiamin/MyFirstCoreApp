using MyFirstCoreApp.Application.Inventory;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IInventoryAnalyticsService
{
    Task<IReadOnlyList<InventoryAlertDto>> GetBelowMinimumStockAsync(int tenantId);
    Task<IReadOnlyList<InventoryValuationDto>> GetInventoryValuationAsync(
        int tenantId, DateOnly asOf, int? warehouseId = null, int? productId = null);
}
