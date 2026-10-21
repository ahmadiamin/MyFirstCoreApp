
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class WarehouseService : IWarehouseService
{
    private readonly ApplicationDbContext _context;

    public WarehouseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetDefaultWarehouseIdAsync(int tenantId)
    {
        var warehouseId = await _context.Warehouses
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                x.IsDefault)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync();

        if (!warehouseId.HasValue)
        {
            throw new InvalidOperationException(
                "برای شرکت جاری، انبار پیش‌فرض فعال تعیین نشده است.");
        }

        return warehouseId.Value;
    }

    public async Task SetDefaultWarehouseAsync(
        int warehouseId,
        int tenantId)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var selectedWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(x =>
                x.Id == warehouseId &&
                x.TenantId == tenantId &&
                x.IsActive);

        if (selectedWarehouse is null)
        {
            throw new InvalidOperationException(
                "انبار انتخاب‌شده وجود ندارد، غیرفعال است یا متعلق به شرکت جاری نیست.");
        }

        var currentDefaults = await _context.Warehouses
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsDefault)
            .ToListAsync();

        foreach (var warehouse in currentDefaults)
        {
            warehouse.IsDefault = false;
        }

        selectedWarehouse.IsDefault = true;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}