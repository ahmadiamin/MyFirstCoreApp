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
    public async Task<IActionResult> Index(
        string? search = null,
        ProjectStatus? status = null)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        var items = await _projectService.GetListAsync(
            tenantId.Value,
            status,
            search);

        ViewBag.Search = search;
        ViewBag.Status = status;

        return View(items);
    }

    // GET: /Projects/Projects/Details/5
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
                StartDate = model.StartDate,
                PlannedEndDate = model.PlannedEndDate
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
            StartDate = project.StartDate,
            PlannedEndDate = project.PlannedEndDate
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
                StartDate = model.StartDate,
                PlannedEndDate = model.PlannedEndDate
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
}