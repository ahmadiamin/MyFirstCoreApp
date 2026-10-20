using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Areas.Invoicing.ViewModels.InvoicePrintSetting;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Invoicing.Controllers;

[Area("Invoicing")]
[Authorize]
public class InvoicePrintSettingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenant _currentTenant;

    public InvoicePrintSettingsController(ApplicationDbContext context, ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    [HttpGet]
    [Authorize(Policy = "InvoicePrintSetting.View")]
    public async Task<IActionResult> Edit()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var setting = await _context.InvoicePrintSettings
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                !x.IsDeleted);

        var model = setting == null
            ? new InvoicePrintSettingViewModel()
            : new InvoicePrintSettingViewModel
            {
                ShowBalance = setting.ShowBalance,
                PoemSelectionMode = setting.PoemSelectionMode,
                SelectedInvoicePoemId = setting.SelectedInvoicePoemId,
                Description = setting.Description,
            };

        await LoadPoemsAsync(model, tenantId.Value);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InvoicePrintSetting.Edit")]
    public async Task<IActionResult> Edit(InvoicePrintSettingViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        if (!Enum.IsDefined(model.PoemSelectionMode))
            ModelState.AddModelError(nameof(model.PoemSelectionMode), "روش انتخاب شعر معتبر نیست.");

        if (model.PoemSelectionMode == InvoicePoemSelectionMode.Selected)
        {
            if (!model.SelectedInvoicePoemId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SelectedInvoicePoemId), "لطفاً شعر موردنظر را انتخاب کنید.");
            }
            else
            {
                var poemExists = await _context.InvoicePoems
                    .AnyAsync(x =>
                        x.Id == model.SelectedInvoicePoemId.Value &&
                        x.TenantId == tenantId.Value &&
                        x.IsActive &&
                        !x.IsDeleted);

                if (!poemExists)
                {
                    ModelState.AddModelError(nameof(model.SelectedInvoicePoemId), "شعر انتخاب‌شده معتبر یا فعال نیست.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadPoemsAsync(model, tenantId.Value);
            return View(model);
        }

        var setting = await _context.InvoicePrintSettings
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                !x.IsDeleted);

        if (setting == null)
        {
            setting = new InvoicePrintSetting
            {
                TenantId = tenantId.Value
            };

            _context.InvoicePrintSettings.Add(setting);
        }

        setting.ShowBalance = model.ShowBalance;
        setting.PoemSelectionMode = model.PoemSelectionMode;
        setting.SelectedInvoicePoemId =
            model.PoemSelectionMode == InvoicePoemSelectionMode.Selected
                ? model.SelectedInvoicePoemId
                : null;
        setting.Description = model.Description;

        await _context.SaveChangesAsync();

        TempData["Success"] = "تنظیمات چاپ با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Edit));
    }

    private async Task LoadPoemsAsync(InvoicePrintSettingViewModel model, int tenantId)
    {
        model.Poems = await _context.InvoicePoems
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive &&
                !x.IsDeleted)
            .OrderBy(x => x.Id)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Text
            })
            .ToListAsync();
    }
}