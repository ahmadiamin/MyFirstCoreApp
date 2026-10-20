using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Areas.Invoicing.ViewModels.InvoicePoem;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Invoicing.Controllers;

[Area("Invoicing")]
[Authorize]
public class InvoicePoemsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public InvoicePoemsController(
        ApplicationDbContext context,
        ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    [Authorize(Policy = "InvoicePoem.View")]
    public async Task<IActionResult> Index()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var poems = await _context.InvoicePoems
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId.Value &&
                !x.IsDeleted)
            .OrderByDescending(x => x.Id)
            .Select(x => new InvoicePoemViewModel
            {
                Id = x.Id,
                Text = x.Text,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return View(poems);
    }

    [HttpGet]
    [Authorize(Policy = "InvoicePoem.Create")]
    public IActionResult Create()
    {
        return View(new InvoicePoemViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InvoicePoem.Create")]
    public async Task<IActionResult> Create(InvoicePoemViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        var poem = new InvoicePoem
        {
            TenantId = tenantId.Value,
            Text = model.Text.Trim(),
            IsActive = model.IsActive
        };

        _context.InvoicePoems.Add(poem);
        await _context.SaveChangesAsync();

        TempData["Success"] = "شعر با موفقیت ثبت شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = "InvoicePoem.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var poem = await _context.InvoicePoems
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId.Value &&
                !x.IsDeleted);

        if (poem == null)
            return NotFound();

        return View(new InvoicePoemViewModel
        {
            Id = poem.Id,
            Text = poem.Text,
            IsActive = poem.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InvoicePoem.Edit")]
    public async Task<IActionResult> Edit(
        int id,
        InvoicePoemViewModel model)
    {
        if (id != model.Id)
            return BadRequest();

        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        var poem = await _context.InvoicePoems
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId.Value &&
                !x.IsDeleted);

        if (poem == null)
            return NotFound();

        poem.Text = model.Text.Trim();
        poem.IsActive = model.IsActive;

        await _context.SaveChangesAsync();

        TempData["Success"] = "شعر با موفقیت ویرایش شد.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InvoicePoem.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var poem = await _context.InvoicePoems
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.TenantId == tenantId.Value &&
                !x.IsDeleted);

        if (poem == null)
            return NotFound();

        // اگر شعر در تنظیمات انتخاب شده باشد، انتخاب ثابت را پاک می‌کنیم.
        var settings = await _context.InvoicePrintSettings
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                !x.IsDeleted &&
                x.SelectedInvoicePoemId == id);

        if (settings != null)
        {
            settings.SelectedInvoicePoemId = null;
        }

        // حذف نرم؛ رکورد از دیتابیس پاک نمی‌شود.
        poem.IsDeleted = true;
        poem.IsActive = false;

        await _context.SaveChangesAsync();

        TempData["Success"] = "شعر حذف شد.";

        return RedirectToAction(nameof(Index));
    }
}