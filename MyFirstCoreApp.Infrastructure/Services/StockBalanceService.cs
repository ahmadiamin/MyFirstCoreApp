using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Inventory;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class StockBalanceService : IStockBalanceService
{
    private readonly ApplicationDbContext _context;

    public StockBalanceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetBalanceAsync(
        int productId,
        int warehouseId,
        int tenantId)
    {
        await ValidateProductAndWarehouseAsync(
            productId, warehouseId, tenantId);

        var rows = await GetWarehouseBalancesQuery(
                warehouseId, tenantId, productId)
            .ToListAsync();

        return rows.FirstOrDefault()?.Quantity ?? 0m;
    }

    public async Task<IReadOnlyList<StockBalanceDto>>
        GetWarehouseBalancesAsync(int warehouseId, int tenantId)
    {
        var warehouse = await _context.Warehouses
            .AsNoTracking()
            .Where(w =>
                w.Id == warehouseId &&
                w.TenantId == tenantId &&
                w.IsActive)
            .Select(w => new { w.Id, w.Name })
            .FirstOrDefaultAsync();

        if (warehouse == null)
            throw new InvalidOperationException("انبار پیدا نشد یا غیرفعال است.");

        var rows = await GetWarehouseBalancesQuery(
                warehouseId, tenantId, null)
            .OrderBy(x => x.ProductName)
            .ToListAsync();

        foreach (var row in rows)
            row.WarehouseName = warehouse.Name;

        return rows;
    }

    public async Task<IReadOnlyList<StockBalanceDto>>
        GetProductBalancesAsync(int productId, int tenantId)
    {
        var product = await _context.Products
            .AsNoTracking()
            .Where(p =>
                p.Id == productId &&
                p.TenantId == tenantId &&
                p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Code, UnitName = p.Unit.Name })
            .FirstOrDefaultAsync();

        if (product == null)
            throw new InvalidOperationException("کالا پیدا نشد یا غیرفعال است.");

        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.IsActive)
            .Select(w => new
            {
                w.Id,
                w.Name
            })
            .ToListAsync();

        // مجموع ورود به هر انبار
        var incomingQuery =
            from item in _context.StockMovementItems.AsNoTracking()
            join movement in _context.StockMovements.AsNoTracking()
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && item.ProductId == productId
                  && movement.Status == StockMovementStatus.Posted
                  && movement.DestinationWarehouseId.HasValue
            group item by movement.DestinationWarehouseId!.Value
            into g
            select new
            {
                WarehouseId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            };

        // مجموع خروج از هر انبار
        var outgoingQuery =
            from item in _context.StockMovementItems.AsNoTracking()
            join movement in _context.StockMovements.AsNoTracking()
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && item.ProductId == productId
                  && movement.Status == StockMovementStatus.Posted
                  && movement.SourceWarehouseId.HasValue
            group item by movement.SourceWarehouseId!.Value
            into g
            select new
            {
                WarehouseId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            };

        var incoming = await incomingQuery.ToListAsync();
        var outgoing = await outgoingQuery.ToListAsync();

        var incomingByWarehouse = incoming.ToDictionary(
            x => x.WarehouseId, x => x.Quantity);

        var outgoingByWarehouse = outgoing.ToDictionary(
            x => x.WarehouseId, x => x.Quantity);

        return warehouses
            .Select(w => new StockBalanceDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductCode = product.Code,
                UnitName = product.UnitName,
                WarehouseId = w.Id,
                WarehouseName = w.Name,
                Quantity =
                    incomingByWarehouse.GetValueOrDefault(w.Id, 0m)
                    - outgoingByWarehouse.GetValueOrDefault(w.Id, 0m)
            })
            .OrderBy(x => x.WarehouseName)
            .ToList();
    }

    public async Task<IReadOnlyList<StockBalanceDto>> GetAllBalancesAsync(
        int tenantId,
        int? warehouseId = null)
    {
        if (warehouseId.HasValue && !await _context.Warehouses.AnyAsync(w =>
                w.Id == warehouseId.Value && w.TenantId == tenantId && w.IsActive))
            throw new InvalidOperationException("انبار پیدا نشد یا غیرفعال است.");

        var incomingQuery =
            from item in _context.StockMovementItems.AsNoTracking()
            join movement in _context.StockMovements.AsNoTracking()
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && movement.Status == StockMovementStatus.Posted
                  && movement.DestinationWarehouseId.HasValue
                  && (!warehouseId.HasValue || movement.DestinationWarehouseId == warehouseId)
            group item by new
            {
                item.ProductId,
                WarehouseId = movement.DestinationWarehouseId!.Value
            }
            into g
            select new { g.Key.ProductId, g.Key.WarehouseId, Quantity = g.Sum(x => x.Quantity) };

        var outgoingQuery =
            from item in _context.StockMovementItems.AsNoTracking()
            join movement in _context.StockMovements.AsNoTracking()
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && movement.Status == StockMovementStatus.Posted
                  && movement.SourceWarehouseId.HasValue
                  && (!warehouseId.HasValue || movement.SourceWarehouseId == warehouseId)
            group item by new
            {
                item.ProductId,
                WarehouseId = movement.SourceWarehouseId!.Value
            }
            into g
            select new { g.Key.ProductId, g.Key.WarehouseId, Quantity = -g.Sum(x => x.Quantity) };

        var movements = await incomingQuery
            .Concat(outgoingQuery)
            .ToListAsync();

        var balances = movements
            .GroupBy(x => new { x.ProductId, x.WarehouseId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseId,
                Quantity = g.Sum(x => x.Quantity)
            })
            .Where(x => x.Quantity != 0)
            .ToList();

        var productIds = balances.Select(x => x.ProductId).Distinct().ToList();
        var warehouseIds = balances.Select(x => x.WarehouseId).Distinct().ToList();
        if (productIds.Count == 0) return [];

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.TrackInventory && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Code, UnitName = p.Unit.Name })
            .ToDictionaryAsync(p => p.Id);

        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.IsActive && warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionaryAsync(w => w.Id);

        return balances
            .Where(x => products.ContainsKey(x.ProductId) && warehouses.ContainsKey(x.WarehouseId))
            .Select(x => new StockBalanceDto
            {
                ProductId = x.ProductId,
                ProductName = products[x.ProductId].Name,
                ProductCode = products[x.ProductId].Code,
                UnitName = products[x.ProductId].UnitName,
                WarehouseId = x.WarehouseId,
                WarehouseName = warehouses[x.WarehouseId].Name,
                Quantity = x.Quantity
            })
            .OrderBy(x => x.WarehouseName)
            .ThenBy(x => x.ProductName)
            .ToList();
    }

    public async Task<IReadOnlyList<DormantStockBalanceDto>> GetDormantBalancesAsync(
        int tenantId,
        DateOnly from,
        DateOnly to,
        int? warehouseId = null,
        int? productId = null)
    {
        if (from > to)
            throw new InvalidOperationException("تاریخ شروع نباید بعد از تاریخ پایان باشد.");

        if (warehouseId.HasValue && !await _context.Warehouses.AnyAsync(w =>
                w.Id == warehouseId.Value && w.TenantId == tenantId && w.IsActive))
            throw new InvalidOperationException("انبار پیدا نشد یا غیرفعال است.");

        if (productId.HasValue && !await _context.Products.AnyAsync(p =>
                p.Id == productId.Value && p.TenantId == tenantId && p.IsActive && p.TrackInventory))
            throw new InvalidOperationException("کالا پیدا نشد یا قابل کنترل موجودی نیست.");

        var incoming =
            from item in _context.StockMovementItems.AsNoTracking()
            join movement in _context.StockMovements.AsNoTracking()
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && movement.Status == StockMovementStatus.Posted
                  && movement.Date <= to
                  && movement.DestinationWarehouseId.HasValue
                  && (!warehouseId.HasValue || movement.DestinationWarehouseId == warehouseId)
                  && (!productId.HasValue || item.ProductId == productId)
            select new
            {
                item.ProductId,
                WarehouseId = movement.DestinationWarehouseId!.Value,
                QuantityDelta = item.Quantity,
                movement.Date
            };

        var outgoing =
            from item in _context.StockMovementItems.AsNoTracking()
            join movement in _context.StockMovements.AsNoTracking()
                on item.StockMovementId equals movement.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && movement.Status == StockMovementStatus.Posted
                  && movement.Date <= to
                  && movement.SourceWarehouseId.HasValue
                  && (!warehouseId.HasValue || movement.SourceWarehouseId == warehouseId)
                  && (!productId.HasValue || item.ProductId == productId)
            select new
            {
                item.ProductId,
                WarehouseId = movement.SourceWarehouseId!.Value,
                QuantityDelta = -item.Quantity,
                movement.Date
            };

        var groupedMovements = await incoming.Concat(outgoing)
            .GroupBy(x => new { x.ProductId, x.WarehouseId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseId,
                Quantity = g.Sum(x => x.QuantityDelta),
                LastMovementDate = g.Max(x => x.Date)
            })
            .Where(x => x.Quantity > 0 && x.LastMovementDate < from)
            .ToListAsync();

        if (groupedMovements.Count == 0) return [];

        var productIds = groupedMovements.Select(x => x.ProductId).Distinct().ToList();
        var warehouseIds = groupedMovements.Select(x => x.WarehouseId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive && p.TrackInventory && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Code, UnitName = p.Unit.Name })
            .ToDictionaryAsync(p => p.Id);
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.IsActive && warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionaryAsync(w => w.Id);

        return groupedMovements
            .Where(x => products.ContainsKey(x.ProductId) && warehouses.ContainsKey(x.WarehouseId))
            .Select(x => new DormantStockBalanceDto
            {
                ProductId = x.ProductId,
                ProductName = products[x.ProductId].Name,
                ProductCode = products[x.ProductId].Code,
                UnitName = products[x.ProductId].UnitName,
                WarehouseId = x.WarehouseId,
                WarehouseName = warehouses[x.WarehouseId].Name,
                Quantity = x.Quantity,
                LastMovementDate = x.LastMovementDate
            })
            .OrderByDescending(x => x.Quantity)
            .ThenBy(x => x.ProductName)
            .ToList();
    }

    private IQueryable<StockBalanceDto> GetWarehouseBalancesQuery(
        int warehouseId,
        int tenantId,
        int? productId)
    {
        var query =
            from item in _context.StockMovementItems.AsNoTracking()
            join movement in _context.StockMovements.AsNoTracking()
                on item.StockMovementId equals movement.Id
            join product in _context.Products.AsNoTracking()
                on item.ProductId equals product.Id
            where item.TenantId == tenantId
                  && movement.TenantId == tenantId
                  && product.TenantId == tenantId
                  && movement.Status == StockMovementStatus.Posted
                  && (movement.SourceWarehouseId == warehouseId
                      || movement.DestinationWarehouseId == warehouseId)
            select new
            {
                item.ProductId,
                product.Name,
                product.Code,
                UnitName = product.Unit.Name,
                item.Quantity,
                IsIncoming =
                    movement.DestinationWarehouseId == warehouseId,
                IsOutgoing =
                    movement.SourceWarehouseId == warehouseId
            };

        if (productId.HasValue)
            query = query.Where(x => x.ProductId == productId.Value);

        return query
            .GroupBy(x => new
            {
                x.ProductId,
                x.Name,
                x.Code,
                x.UnitName
            })
            .Select(g => new StockBalanceDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                ProductCode = g.Key.Code,
                UnitName = g.Key.UnitName,
                WarehouseId = warehouseId,
                WarehouseName = string.Empty,
                Quantity =
                    g.Sum(x => x.IsIncoming ? x.Quantity : 0m)
                    - g.Sum(x => x.IsOutgoing ? x.Quantity : 0m)
            });
    }

    private async Task ValidateProductAndWarehouseAsync(
        int productId,
        int warehouseId,
        int tenantId)
    {
        var productExists = await _context.Products
            .AnyAsync(p =>
                p.Id == productId &&
                p.TenantId == tenantId &&
                p.IsActive);

        if (!productExists)
            throw new InvalidOperationException("کالا پیدا نشد یا غیرفعال است.");

        var warehouseExists = await _context.Warehouses
            .AnyAsync(w =>
                w.Id == warehouseId &&
                w.TenantId == tenantId &&
                w.IsActive);

        if (!warehouseExists)
            throw new InvalidOperationException("انبار پیدا نشد یا غیرفعال است.");
    }
}
