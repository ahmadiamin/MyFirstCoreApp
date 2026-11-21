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
namespace MyFirstCoreApp.Areas.Projects.Controllers;

[Area("Projects")]
[Authorize]
public class ProjectManagementController : Controller
{
    private readonly IProjectService _projectService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ApplicationDbContext _context;

    public ProjectManagementController(
        IProjectService projectService,
        ICurrentTenant currentTenant,
        ApplicationDbContext context)
    {
        _projectService = projectService;
        _currentTenant = currentTenant;
        _context = context;
    }

    // ============================================
    // Members
    // ============================================

    [HttpGet]
    [Authorize(Policy = "ProjectMember.Manage")]
    public async Task<IActionResult> AddMember(int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var project = await _projectService.GetByIdAsync(projectId, tenantId.Value);
        if (project is null) return NotFound();

        var model = new ProjectMemberFormViewModel
        {
            ProjectId = projectId
        };

        await PopulateUserOptionsAsync(model, tenantId.Value);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ProjectMember.Manage")]
    public async Task<IActionResult> AddMember(ProjectMemberFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateUserOptionsAsync(model, tenantId.Value);
            return View(model);
        }

        try
        {
            await _projectService.AddMemberAsync(
                new ProjectMemberRequest
                {
                    ProjectId = model.ProjectId,
                    UserTenantId = model.UserTenantId,
                    RoleTitle = model.RoleTitle,
                    Notes = model.Notes
                },
                tenantId.Value);

            TempData["Success"] = "عضو با موفقیت به پروژه اضافه شد.";

            return RedirectToAction(
                "Details",
                "Projects",
                new { area = "Projects", id = model.ProjectId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateUserOptionsAsync(model, tenantId.Value);
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Policy = "ProjectMember.Manage")]
    public async Task<IActionResult> EditMember(int memberId, int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var project = await _projectService.GetByIdAsync(projectId, tenantId.Value);
        if (project is null) return NotFound();

        var member = project.Members.FirstOrDefault(x => x.Id == memberId);
        if (member is null) return NotFound();

        return View(new ProjectMemberFormViewModel
        {
            Id = member.Id,
            ProjectId = projectId,
            UserTenantId = member.UserTenantId,
            UserName = member.UserName,
            RoleTitle = member.RoleTitle,
            Notes = member.Notes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ProjectMember.Manage")]
    public async Task<IActionResult> EditMember(ProjectMemberFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await _projectService.UpdateMemberAsync(
                model.Id,
                model.RoleTitle,
                model.Notes,
                tenantId.Value);

            TempData["Success"] = "اطلاعات عضو پروژه به‌روزرسانی شد.";
            return RedirectToAction("Details", "Projects", new { area = "Projects", id = model.ProjectId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ProjectMember.Manage")]
    public async Task<IActionResult> RemoveMember(int memberId, int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue)
            return Forbid();

        try
        {
            await _projectService.RemoveMemberAsync(memberId, tenantId.Value);

            TempData["Success"] = "عضو از پروژه حذف شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(
            "Details",
            "Projects",
            new { area = "Projects", id = projectId });
    }

    // ============================================
    // Stages
    // ============================================

    [HttpGet]
    [Authorize(Policy = "ProjectStage.Manage")]
    public async Task<IActionResult> CreateStage(int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var project = await _projectService.GetByIdAsync(projectId, tenantId.Value);
        if (project is null) return NotFound();

        var model = new ProjectStageFormViewModel
        {
            ProjectId = projectId,
            SortOrder = project.Stages.Count + 1
        };

        await PopulateUserOptionsAsync(model, tenantId.Value);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ProjectStage.Manage")]
    public async Task<IActionResult> CreateStage(ProjectStageFormViewModel model)
    {
        var plannedStartDate = ParseOptionalPersianDate(model.PlannedStartDate, nameof(model.PlannedStartDate));
        var plannedEndDate = ParseOptionalPersianDate(model.PlannedEndDate, nameof(model.PlannedEndDate));

        if (plannedStartDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate < plannedStartDate)
        {
            ModelState.AddModelError(
                nameof(model.PlannedEndDate),
                "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");
        }
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateUserOptionsAsync(model, tenantId.Value);
            return View(model);
        }

        try
        {
            await _projectService.AddStageAsync(
                new ProjectStageRequest
                {
                    ProjectId = model.ProjectId,
                    Title = model.Title,
                    Description = model.Description,
                    SortOrder = model.SortOrder,
                    IsRequired = model.IsRequired,
                    AssignedUserTenantId = model.AssignedUserTenantId,
                    PlannedStartDate = plannedStartDate,
                    PlannedEndDate = plannedEndDate,
                    Notes = model.Notes
                },
                tenantId.Value);

            TempData["Success"] = "مرحله با موفقیت اضافه شد.";

            return RedirectToProjectDetails(model.ProjectId);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateUserOptionsAsync(model, tenantId.Value);
            return View(model);
        }
    }

    [HttpGet]
    [Authorize(Policy = "ProjectStage.Manage")]
    public async Task<IActionResult> EditStage(int id, int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var project = await _projectService.GetByIdAsync(projectId, tenantId.Value);
        if (project is null) return NotFound();

        var stage = project.Stages.FirstOrDefault(x => x.Id == id);
        if (stage is null) return NotFound();

        var model = new ProjectStageFormViewModel
        {
            Id = stage.Id,
            ProjectId = projectId,
            Title = stage.Title,
            Description = stage.Description,
            SortOrder = stage.SortOrder,
            IsRequired = stage.IsRequired,
            AssignedUserTenantId = stage.AssignedUserTenantId,
            PlannedStartDate = PersianDate.Format(stage.PlannedStartDate),
            PlannedEndDate = PersianDate.Format(stage.PlannedEndDate),
            Notes = stage.Notes
        };

        await PopulateUserOptionsAsync(model, tenantId.Value);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ProjectStage.Manage")]
    public async Task<IActionResult> EditStage(ProjectStageFormViewModel model)
    {
        var plannedStartDate = ParseOptionalPersianDate(model.PlannedStartDate, nameof(model.PlannedStartDate));
        var plannedEndDate = ParseOptionalPersianDate(model.PlannedEndDate, nameof(model.PlannedEndDate));

        if (plannedStartDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate < plannedStartDate)
        {
            ModelState.AddModelError(
                nameof(model.PlannedEndDate),
                "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.");
        }
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateUserOptionsAsync(model, tenantId.Value);
            return View(model);
        }

        try
        {
            await _projectService.UpdateStageAsync(
                new ProjectStageRequest
                {
                    Id = model.Id,
                    ProjectId = model.ProjectId,
                    Title = model.Title,
                    Description = model.Description,
                    SortOrder = model.SortOrder,
                    IsRequired = model.IsRequired,
                    AssignedUserTenantId = model.AssignedUserTenantId,
                    PlannedStartDate = plannedStartDate,
                    PlannedEndDate = plannedEndDate,
                    Notes = model.Notes
                },
                tenantId.Value);

            TempData["Success"] = "مرحله با موفقیت ویرایش شد.";

            return RedirectToProjectDetails(model.ProjectId);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateUserOptionsAsync(model, tenantId.Value);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ProjectStage.Manage")]
    public async Task<IActionResult> DeleteStage(int id, int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        try
        {
            await _projectService.DeleteStageAsync(id, tenantId.Value);
            TempData["Success"] = "مرحله با موفقیت حذف شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToProjectDetails(projectId);
    }

    // ============================================
    // Stage Status
    // ============================================

    [HttpGet]
    [Authorize(Policy = "ProjectStage.Status.Change")]
    public async Task<IActionResult> ChangeStageStatus(int id, int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var project = await _projectService.GetByIdAsync(projectId, tenantId.Value);
        if (project is null) return NotFound();

        var stage = project.Stages.FirstOrDefault(x => x.Id == id);
        if (stage is null) return NotFound();

        return View(new ProjectStageStatusFormViewModel
        {
            ProjectId = projectId,
            ProjectStageId = id,
            NewStatus = stage.Status,
            NextFollowUpDate = PersianDate.Format(stage.NextFollowUpDate)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ProjectStage.Status.Change")]
    public async Task<IActionResult> ChangeStageStatus(
        ProjectStageStatusFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();
        DateOnly? nextFollowUpDate = null;

        if (!string.IsNullOrWhiteSpace(model.NextFollowUpDate))
        {
            if (PersianDate.TryParse(model.NextFollowUpDate, out var parsedDate))
            {
                nextFollowUpDate = parsedDate;
            }
            else
            {
                ModelState.AddModelError(
                    nameof(model.NextFollowUpDate),
                    "تاریخ شمسی نامعتبر است. نمونه صحیح: ۱۴۰۵/۰۱/۱۵");
            }
        }
        if (model.NewStatus == ProjectStageStatus.Waiting)
        {
            if (string.IsNullOrWhiteSpace(model.WaitingReason))
            {
                ModelState.AddModelError(
                    nameof(model.WaitingReason),
                    "برای وضعیت در انتظار، علت انتظار الزامی است.");
            }

            if (!nextFollowUpDate.HasValue)
            {
                ModelState.AddModelError(
                    nameof(model.NextFollowUpDate),
                    "برای وضعیت در انتظار، تاریخ پیگیری الزامی است.");
            }
        }

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await _projectService.ChangeStageStatusAsync(
                new ProjectStageStatusChangeRequest
                {
                    ProjectStageId = model.ProjectStageId,
                    NewStatus = model.NewStatus,
                    Description = model.Description,
                    WaitingReason = model.WaitingReason,
                    NextFollowUpDate = nextFollowUpDate
                },
                tenantId.Value);

            TempData["Success"] = "وضعیت مرحله با موفقیت تغییر کرد.";
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        return RedirectToProjectDetails(model.ProjectId);
    }

    // ============================================
    // Project Status
    // ============================================

    [HttpGet]
    [Authorize(Policy = "Project.Status.Change")]
    public async Task<IActionResult> ChangeProjectStatus(int projectId)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        var project = await _projectService.GetByIdAsync(projectId, tenantId.Value);
        if (project is null) return NotFound();

        return View(new ProjectStatusFormViewModel
        {
            ProjectId = projectId,
            NewStatus = project.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Project.Status.Change")]
    public async Task<IActionResult> ChangeProjectStatus(
        ProjectStatusFormViewModel model)
    {
        var tenantId = _currentTenant.TenantId;
        if (!tenantId.HasValue) return Forbid();

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            await _projectService.ChangeStatusAsync(
                new ProjectStatusChangeRequest
                {
                    ProjectId = model.ProjectId,
                    NewStatus = model.NewStatus,
                    HoldReason = model.HoldReason,
                    CancellationReason = model.CancellationReason
                },
                tenantId.Value);

            TempData["Success"] = "وضعیت پروژه با موفقیت تغییر کرد.";
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        return RedirectToProjectDetails(model.ProjectId);
    }

    // ============================================
    // Helpers
    // ============================================
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
    private IActionResult RedirectToProjectDetails(int projectId)
    {
        return RedirectToAction(
            "Details",
            "Projects",
            new { area = "Projects", id = projectId });
    }

    private async Task PopulateUserOptionsAsync(
        ProjectMemberFormViewModel model,
        int tenantId)
    {
        model.UserOptions = await (
            from membership in _context.UserTenants.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.TenantId == tenantId && user.IsActive
            orderby membership.FirstName, membership.LastName
            select new SelectListItem
            {
                Value = membership.Id.ToString(),
                Text = ((membership.FirstName ?? "") + " " +
                        (membership.LastName ?? "")).Trim() == ""
                    ? user.FullName
                    : ((membership.FirstName ?? "") + " " +
                       (membership.LastName ?? "")).Trim()
            }
        ).ToListAsync();
    }

    private async Task PopulateUserOptionsAsync(
        ProjectStageFormViewModel model,
        int tenantId)
    {
        model.UserOptions = await (
            from membership in _context.UserTenants.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.TenantId == tenantId && user.IsActive
            orderby membership.FirstName, membership.LastName
            select new SelectListItem
            {
                Value = membership.Id.ToString(),
                Text = ((membership.FirstName ?? "") + " " +
                        (membership.LastName ?? "")).Trim() == ""
                    ? user.FullName
                    : ((membership.FirstName ?? "") + " " +
                       (membership.LastName ?? "")).Trim()
            }
        ).ToListAsync();
    }

    private IQueryable<(int Id, string Name)> GetUserOptionsQuery(int tenantId)
    {
        // این متد در ادامه با کوئری مستقیم قابل ترجمه به SQL جایگزین می‌شود.
        throw new NotImplementedException();
    }
}
