namespace MyFirstCoreApp.Application.Interfaces;

public interface IWarehouseService
{
    Task<int> GetDefaultWarehouseIdAsync(int tenantId);

    Task SetDefaultWarehouseAsync(
        int warehouseId,
        int tenantId);
}