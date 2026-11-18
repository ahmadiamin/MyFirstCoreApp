using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Application.Projects;
using MyFirstCoreApp.Areas.Projects.ViewModels.Project;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Common.Models;

namespace MyFirstCoreApp.Areas.Projects.Controllers;

[Area("Projects")]
[Authorize]
public class ProjectsController : Controller
{
    private readonly IProjectService _projectService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ApplicationDbContext _context;

    public ProjectsController(
        IProjectService projectService,
        ICurrentTenant currentTenant,
        ApplicationDbContext context)
    {
        _projectService = projectService;
        _currentTenant = currentTenant;
        _context = context;
    }

    // GET: /Projects/Projects
    [Authorize(Policy = "Project.View")]
    public IActionResult Index()
    {
        return View();
    }
    // POST: /Projects/Projects/GetData
    // POST: /Projects/Projects/GetData
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Project.View")]
    public async Task<IActionResult> GetData([FromBody] AgGridRequest request, ProjectStatus? status = null)
    {
        var tenantId = _currentTenant.TenantId;

        if (!tenantId.HasValue)
            return Forbid();

        request ??= new AgGridRequest();

        var items = await _projectService.GetListAsync(
            tenantId.Value,
            status,
            request.Search);

        var total = items.Count;

        // مرتب‌سازی
        items = request.SortBy switch
        {
            "title" => request.SortDir == "desc"
                ? items.OrderByDescending(x => x.Title).ToList()
                : items.OrderBy(x => x.Title).ToList(),

            "code" => request.SortDir == "desc"
                ? items.OrderByDescending(x => x.Code).ToList()
                : items.OrderBy(x => x.Code).ToList(),

            "contactName" => request.SortDir == "desc"
                ? items.OrderByDescending(x => x.ContactName).ToList()
                : items.OrderBy(x => x.ContactName).ToList(),

            "projectManagerName" => request.SortDir == "desc"
                ? items.OrderByDescending(x => x.ProjectManagerName).ToList()
                : items.OrderBy(x => x.ProjectManagerName).ToList(),

            "startDate" => request.SortDir == "desc"
                ? items.OrderByDescending(x => x.StartDate).ToList()
                : items.OrderBy(x => x.StartDate).ToList(),

            "plannedEndDate" => request.SortDir == "desc"
                ? items.OrderByDescending(x => x.PlannedEndDate).ToList()
                : items.OrderBy(x => x.PlannedEndDate).ToList(),

            _ => items.OrderByDescending(x => x.Id).ToList()
        };

        var rows = items
            .Skip(Math.Max(0, request.StartRow))
            .Take(Math.Max(0, request.EndRow - request.StartRow))
            .Select(x => new
            {
                id = x.Id,
                title = x.Title,
                code = x.Code,
                contactName = x.ContactName,
                projectManagerName = x.ProjectManagerName,
                status = (int)x.Status,
                stageCount = x.StageCount,
                completedStageCount = x.CompletedStageCount,
                startDate = x.StartDate?.ToString("yyyy/MM/dd"),
                plannedEndDate = x.PlannedEndDate?.ToString("yyyy/MM/dd")
            })
            .ToList();

        return Json(new
        {
            success = true,
            rows,
            lastRow = total
        });
    }

    [Authorize(Policy = "Project.View")]
    public async Task<IActionResult> Details(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var project = await _projectService.GetByIdAsync(
            id,
            tenantId.Value);

        if (project is null)
            return NotFound();

        return View(project);
    }

    // GET: /Projects/Projects/Create
    [Authorize(Policy = "Project.Create")]
    public async Task<IActionResult> Create()
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var model = new ProjectFormViewModel();

        await PopulateOptionsAsync(model, tenantId.Value);

        return View(model);
    }

    // POST: /Projects/Projects/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Project.Create")]
    public async Task<IActionResult> Create(ProjectFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, tenantId.Value);
            return View(model);
        }
        var startDate = ParseOptionalPersianDate(
    model.StartDate,
    nameof(model.StartDate));

        var plannedEndDate = ParseOptionalPersianDate(
            model.PlannedEndDate,
            nameof(model.PlannedEndDate));

        if (startDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate.Value < startDate.Value)
        {
            ModelState.AddModelError(
                nameof(model.PlannedEndDate),
                "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");
        }
        try
        {
            var request = new ProjectRequest
            {
                Title = model.Title,
                Code = model.Code,
                ContactId = model.ContactId,
                ProjectManagerUserTenantId = model.ProjectManagerUserTenantId,
                Address = model.Address,
                Description = model.Description,
                StartDate = startDate,
                PlannedEndDate = plannedEndDate
            };

            var id = await _projectService.CreateAsync(
                request,
                tenantId.Value);

            TempData["Success"] = "پروژه با موفقیت ایجاد شد.";

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        await PopulateOptionsAsync(model, tenantId.Value);
        return View(model);
    }

    // GET: /Projects/Projects/Edit/5
    [Authorize(Policy = "Project.Edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var project = await _projectService.GetByIdAsync(
            id,
            tenantId.Value);

        if (project is null)
            return NotFound();

        var model = new ProjectFormViewModel
        {
            Id = project.Id,
            Title = project.Title,
            Code = project.Code,
            ContactId = project.ContactId,
            ProjectManagerUserTenantId = project.ProjectManagerUserTenantId,
            Address = project.Address,
            Description = project.Description,
            StartDate = PersianDate.Format(project.StartDate),
            PlannedEndDate = PersianDate.Format(project.PlannedEndDate)
        };

        await PopulateOptionsAsync(model, tenantId.Value);

        return View(model);
    }

    // POST: /Projects/Projects/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Project.Edit")]
    public async Task<IActionResult> Edit(
        int id,
        ProjectFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, tenantId.Value);
            return View(model);
        }
        var startDate = ParseOptionalPersianDate(
    model.StartDate,
    nameof(model.StartDate));

        var plannedEndDate = ParseOptionalPersianDate(
            model.PlannedEndDate,
            nameof(model.PlannedEndDate));

        if (startDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate.Value < startDate.Value)
        {
            ModelState.AddModelError(
                nameof(model.PlannedEndDate),
                "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");
        }
        try
        {
            var request = new ProjectRequest
            {
                Id = model.Id,
                Title = model.Title,
                Code = model.Code,
                ContactId = model.ContactId,
                ProjectManagerUserTenantId = model.ProjectManagerUserTenantId,
                Address = model.Address,
                Description = model.Description,
                StartDate = startDate,
                PlannedEndDate = plannedEndDate
            };

            await _projectService.UpdateAsync(
                request,
                tenantId.Value);

            TempData["Success"] = "اطلاعات پروژه با موفقیت ویرایش شد.";

            return RedirectToAction(nameof(Details), new { id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        await PopulateOptionsAsync(model, tenantId.Value);
        return View(model);
    }

    // POST: /Projects/Projects/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Project.Delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        try
        {
            await _projectService.DeleteAsync(id, tenantId.Value);

            return Json(new
            {
                success = true,
                message = "پروژه با موفقیت حذف شد."
            });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new
            {
                success = false,
                message = ex.Message
            });
        }
    }

    // ---------- Helpers ----------

    private async Task PopulateOptionsAsync(
        ProjectFormViewModel model,
        int tenantId)
    {
        model.ContactOptions = await _context.Contacts
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();

        model.ProjectManagerOptions = await (
            from membership in _context.UserTenants.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.TenantId == tenantId
                  && user.IsActive
            orderby membership.FirstName, membership.LastName
            select new SelectListItem
            {
                Value = membership.Id.ToString(),
                Text = (
                    (membership.FirstName ?? "") + " " +
                    (membership.LastName ?? "")
                ).Trim() == ""
                    ? user.FullName
                    : (
                        (membership.FirstName ?? "") + " " +
                        (membership.LastName ?? "")
                    ).Trim()
            })
            .ToListAsync();
    }

    private DateOnly? ParseOptionalPersianDate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (PersianDate.TryParse(value, out var date))
            return date;

        ModelState.AddModelError(
            fieldName,
            "تاریخ شمسی نامعتبر است. نمونه صحیح: ۱۴۰۵/۰۱/۱۵");

        return null;
    }
}