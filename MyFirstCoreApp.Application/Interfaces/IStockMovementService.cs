using MyFirstCoreApp.Application.Inventory;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IStockMovementService
{
    Task<int> CreateAsync(
        StockMovementRequest request,
        int tenantId);

    Task UpdateAsync(
        StockMovementRequest request,
        int tenantId);

    Task PostAsync(
        int id,
        int tenantId);

    Task CancelAsync(
        int id,
        int tenantId);

    Task ReverseAsync(int id, int tenantId);

    Task DeleteAsync(int id, int tenantId, string? deletedByUserId);

    Task CreateFromInvoiceAsync(int invoiceId, int tenantId);
    Task ReverseInvoiceAsync(int invoiceId, int tenantId);
    Task<int> AdjustBalanceAsync(
        int productId,
        int warehouseId,
        decimal targetQuantity,
        decimal? unitCost,
        DateOnly date,
        int tenantId);
    Task<decimal> GetBalanceAsync(
    int productId,
    int warehouseId,
    int tenantId);

    Task<List<KardexRowDto>> GetKardexAsync(
    int productId,
    int warehouseId,
    DateOnly? from,
    DateOnly? to,
    int tenantId);
}
