using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Areas.Inventory.ViewModels.StockBalances;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Inventory.Controllers;

[Area("Inventory")]
[Authorize]
public sealed class StockBalancesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IStockBalanceService _stockBalanceService;

    public StockBalancesController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant,
        IStockBalanceService stockBalanceService)
    {
        _context = context;
        _currentTenant = currentTenant;
        _stockBalanceService = stockBalanceService;
    }

    [Authorize(Policy = "StockBalance.View")]
    public async Task<IActionResult> Index(int? warehouseId, bool search = false)
    {
        if (!_currentTenant.TenantId.HasValue) return Forbid();
        var tenantId = _currentTenant.TenantId.Value;
        var warehouses = await _context.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync();

        var model = new StockBalanceReportViewModel
        {
            WarehouseId = warehouseId,
            HasSearched = search,
            Warehouses = warehouses
                .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
                .ToList()
        };

        if (search)
        {
            try
            {
                model.Rows = await _stockBalanceService.GetAllBalancesAsync(tenantId, warehouseId);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                model.WarehouseId = null;
            }
        }

        return View(model);
    }
}
