using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Projects;
using MyFirstCoreApp.Domain.Entities.Projects;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Infrastructure.Services;

public partial class ProjectService
{
    // ============================================
    // Project Members
    // ============================================

    public async Task<int> AddMemberAsync(
        ProjectMemberRequest request,
        int tenantId)
    {
        var project = await GetEditableProjectAsync(
            request.ProjectId, tenantId);

        await ValidateUserTenantAsync(request.UserTenantId, tenantId);

        var alreadyExists = await _context.Set<ProjectMember>()
            .AnyAsync(x =>
                x.ProjectId == request.ProjectId &&
                x.UserTenantId == request.UserTenantId &&
                x.TenantId == tenantId);

        if (alreadyExists)
            throw new InvalidOperationException(
                "این کاربر قبلاً عضو پروژه شده است.");

        var member = new ProjectMember
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            UserTenantId = request.UserTenantId,
            RoleTitle = NormalizeOptional(request.RoleTitle),
            Notes = NormalizeOptional(request.Notes)
        };

        _context.Set<ProjectMember>().Add(member);
        await _context.SaveChangesAsync();

        return member.Id;
    }

    public async Task RemoveMemberAsync(
        int memberId,
        int tenantId)
    {
        var member = await _context.Set<ProjectMember>()
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x =>
                x.Id == memberId &&
                x.TenantId == tenantId);

        if (member is null)
            throw new InvalidOperationException("عضو پروژه پیدا نشد.");

        EnsureProjectIsEditable(member.Project);

        member.IsDeleted = true;
        member.DeletedAt = DateTime.UtcNow;
        member.DeletedBy = _currentUser.UserId;

        await _context.SaveChangesAsync();
    }

    public async Task UpdateMemberAsync(
        int memberId,
        string? roleTitle,
        string? notes,
        int tenantId)
    {
        var member = await _context.Set<ProjectMember>()
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x =>
                x.Id == memberId &&
                x.TenantId == tenantId);

        if (member is null)
            throw new InvalidOperationException("عضو پروژه پیدا نشد.");

        EnsureProjectIsEditable(member.Project);

        if (roleTitle?.Trim().Length > 100)
            throw new InvalidOperationException("نقش پروژه نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.");

        if (notes?.Trim().Length > 1000)
            throw new InvalidOperationException("توضیحات نمی‌تواند بیشتر از ۱۰۰۰ کاراکتر باشد.");

        member.RoleTitle = NormalizeOptional(roleTitle);
        member.Notes = NormalizeOptional(notes);

        await _context.SaveChangesAsync();
    }

    // ============================================
    // Project Stages
    // ============================================

    public async Task<int> AddStageAsync(
        ProjectStageRequest request,
        int tenantId)
    {
        ValidateStageRequest(request);

        var project = await GetEditableProjectAsync(
            request.ProjectId, tenantId);

        await ValidateUserTenantAsync(
            request.AssignedUserTenantId, tenantId);

        var stage = new ProjectStage
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            SortOrder = request.SortOrder,
            IsRequired = request.IsRequired,
            AssignedUserTenantId = request.AssignedUserTenantId,
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            Notes = NormalizeOptional(request.Notes),
            Status = ProjectStageStatus.NotStarted
        };

        _context.Set<ProjectStage>().Add(stage);
        await _context.SaveChangesAsync();

        return stage.Id;
    }

    public async Task UpdateStageAsync(
        ProjectStageRequest request,
        int tenantId)
    {
        if (request.Id <= 0)
            throw new InvalidOperationException(
                "شناسه مرحله معتبر نیست.");

        ValidateStageRequest(request);

        var stage = await _context.Set<ProjectStage>()
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x =>
                x.Id == request.Id &&
                x.TenantId == tenantId);

        if (stage is null)
            throw new InvalidOperationException("مرحله پروژه پیدا نشد.");

        EnsureProjectIsEditable(stage.Project);

        if (stage.ProjectId != request.ProjectId)
            throw new InvalidOperationException(
                "مرحله انتخاب‌شده متعلق به این پروژه نیست.");

        if (stage.Status is ProjectStageStatus.Completed
            or ProjectStageStatus.Skipped
            or ProjectStageStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "مرحله نهایی‌شده قابل ویرایش نیست.");
        }

        await ValidateUserTenantAsync(
            request.AssignedUserTenantId, tenantId);

        stage.Title = request.Title.Trim();
        stage.Description = NormalizeOptional(request.Description);
        stage.SortOrder = request.SortOrder;
        stage.IsRequired = request.IsRequired;
        stage.AssignedUserTenantId = request.AssignedUserTenantId;
        stage.PlannedStartDate = request.PlannedStartDate;
        stage.PlannedEndDate = request.PlannedEndDate;
        stage.Notes = NormalizeOptional(request.Notes);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteStageAsync(
        int stageId,
        int tenantId)
    {
        var stage = await _context.Set<ProjectStage>()
            .Include(x => x.Project)
            .Include(x => x.Histories)
            .FirstOrDefaultAsync(x =>
                x.Id == stageId &&
                x.TenantId == tenantId);

        if (stage is null)
            throw new InvalidOperationException("مرحله پروژه پیدا نشد.");

        EnsureProjectIsEditable(stage.Project);

        if (stage.Status != ProjectStageStatus.NotStarted ||
            stage.Histories.Any())
        {
            throw new InvalidOperationException(
                "فقط مرحله‌ای که هنوز شروع نشده و سابقه تغییر وضعیت ندارد قابل حذف است.");
        }

        stage.IsDeleted = true;
        stage.DeletedAt = DateTime.UtcNow;
        stage.DeletedBy = _currentUser.UserId;

        await _context.SaveChangesAsync();
    }

    public async Task ChangeStageStatusAsync(
        ProjectStageStatusChangeRequest request,
        int tenantId)
    {
        var stage = await _context.Set<ProjectStage>()
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x =>
                x.Id == request.ProjectStageId &&
                x.TenantId == tenantId);

        if (stage is null)
            throw new InvalidOperationException("مرحله پروژه پیدا نشد.");

        if (stage.Project.Status != ProjectStatus.Active)
            throw new InvalidOperationException(
                "تغییر وضعیت مراحل فقط در پروژه فعال امکان‌پذیر است.");

        if (!Enum.IsDefined(typeof(ProjectStageStatus), request.NewStatus))
            throw new InvalidOperationException("وضعیت مرحله معتبر نیست.");

        if (stage.Status == request.NewStatus)
            throw new InvalidOperationException(
                "مرحله در وضعیت انتخاب‌شده قرار دارد.");

        var allowed = stage.Status switch
        {
            ProjectStageStatus.NotStarted =>
                request.NewStatus is ProjectStageStatus.InProgress
                    or ProjectStageStatus.Waiting
                    or ProjectStageStatus.Skipped
                    or ProjectStageStatus.Cancelled,

            ProjectStageStatus.InProgress =>
                request.NewStatus is ProjectStageStatus.Waiting
                    or ProjectStageStatus.Completed
                    or ProjectStageStatus.Cancelled,

            ProjectStageStatus.Waiting =>
                request.NewStatus is ProjectStageStatus.InProgress
                    or ProjectStageStatus.Completed
                    or ProjectStageStatus.Cancelled,

            _ => false
        };

        if (!allowed)
            throw new InvalidOperationException(
                "تغییر وضعیت مرحله از وضعیت فعلی به وضعیت انتخاب‌شده مجاز نیست.");

        if (request.NewStatus == ProjectStageStatus.Waiting)
        {
            if (string.IsNullOrWhiteSpace(request.WaitingReason))
                throw new InvalidOperationException(
                    "علت انتظار الزامی است.");

            if (!request.NextFollowUpDate.HasValue)
                throw new InvalidOperationException(
                    "تاریخ پیگیری بعدی الزامی است.");

            stage.WaitingReason = request.WaitingReason.Trim();
            stage.NextFollowUpDate = request.NextFollowUpDate;
        }
        else
        {
            stage.WaitingReason = null;
            stage.NextFollowUpDate = null;
        }

        var now = DateTime.UtcNow;

        if (request.NewStatus == ProjectStageStatus.InProgress)
            stage.StartedAt ??= now;

        if (request.NewStatus == ProjectStageStatus.Completed)
            stage.CompletedAt = now;

        var previousStatus = stage.Status;

        stage.Status = request.NewStatus;

        _context.Set<ProjectStageHistory>().Add(
            new ProjectStageHistory
            {
                TenantId = tenantId,
                ProjectStageId = stage.Id,
                PreviousStatus = previousStatus,
                NewStatus = request.NewStatus,
                Description = NormalizeOptional(request.Description),
                ChangedByUserId = _currentUser.UserId,
                ChangedAt = now
            });

        await _context.SaveChangesAsync();
    }

    public async Task AddStageReportAsync(ProjectStageReportRequest request, int tenantId)
    {
        if (request.ReportDate == default)
            throw new InvalidOperationException("تاریخ گزارش معتبر نیست.");
        if (request.ProgressPercent is < 0 or > 100)
            throw new InvalidOperationException("درصد پیشرفت باید بین صفر و صد باشد.");
        if (string.IsNullOrWhiteSpace(request.WorkSummary))
            throw new InvalidOperationException("شرح کار انجام‌شده الزامی است.");
        if (request.IsStopped && string.IsNullOrWhiteSpace(request.StopReason))
            throw new InvalidOperationException("در صورت توقف، علت توقف الزامی است.");
        if (request.IsStopped && !request.NextFollowUpDate.HasValue)
            throw new InvalidOperationException("برای توقف، تاریخ پیگیری بعدی را مشخص کنید.");
        if (request.NextFollowUpDate < request.ReportDate)
            throw new InvalidOperationException("تاریخ پیگیری نمی‌تواند قبل از تاریخ گزارش باشد.");

        var stage = await _context.Set<ProjectStage>()
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == request.ProjectStageId && x.TenantId == tenantId);
        if (stage == null)
            throw new InvalidOperationException("مرحله پروژه پیدا نشد.");
        if (stage.Project.Status != ProjectStatus.Active)
            throw new InvalidOperationException("گزارش اجرا فقط برای پروژه فعال ثبت می‌شود.");
        if (stage.Status is ProjectStageStatus.Completed or ProjectStageStatus.Skipped or ProjectStageStatus.Cancelled)
            throw new InvalidOperationException("برای مرحله تکمیل‌شده، ردشده یا لغوشده گزارش جدید ثبت نمی‌شود.");

        var previousStatus = stage.Status;
        var nextStatus = request.IsStopped
            ? ProjectStageStatus.Waiting
            : stage.Status == ProjectStageStatus.Waiting
                ? ProjectStageStatus.InProgress
                : stage.Status;
        var now = DateTime.UtcNow;
        if (request.ProgressPercent > 0)
            stage.StartedAt ??= now;
        stage.Status = nextStatus;
        stage.WaitingReason = request.IsStopped ? request.StopReason!.Trim() : null;
        stage.NextFollowUpDate = request.NextFollowUpDate;

        _context.Set<ProjectStageReport>().Add(new ProjectStageReport
        {
            TenantId = tenantId,
            ProjectStageId = stage.Id,
            ReportDate = request.ReportDate,
            ProgressPercent = request.ProgressPercent,
            WorkSummary = request.WorkSummary.Trim(),
            IsStopped = request.IsStopped,
            StopReason = NormalizeOptional(request.StopReason),
            NextFollowUpDate = request.NextFollowUpDate,
            ReportedByUserId = NormalizeOptional(request.ReportedByUserId)
        });

        if (nextStatus != previousStatus)
        {
            _context.Set<ProjectStageHistory>().Add(new ProjectStageHistory
            {
                TenantId = tenantId,
                ProjectStageId = stage.Id,
                PreviousStatus = previousStatus,
                NewStatus = nextStatus,
                Description = request.IsStopped ? $"گزارش اجرا؛ توقف: {request.StopReason!.Trim()}" : "گزارش اجرا؛ ادامه فعالیت",
                ChangedByUserId = request.ReportedByUserId,
                ChangedAt = now
            });
        }

        await _context.SaveChangesAsync();
    }

    // ============================================
    // Helpers
    // ============================================

    private async Task<Project> GetEditableProjectAsync(
        int projectId,
        int tenantId)
    {
        var project = await _context.Set<Project>()
            .FirstOrDefaultAsync(x =>
                x.Id == projectId &&
                x.TenantId == tenantId);

        if (project is null)
            throw new InvalidOperationException("پروژه پیدا نشد.");

        EnsureProjectIsEditable(project);

        return project;
    }

    private static void EnsureProjectIsEditable(Project project)
    {
        if (project.Status is ProjectStatus.Completed
            or ProjectStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "پروژه تکمیل‌شده یا لغوشده قابل تغییر نیست.");
        }
    }

    private static void ValidateStageRequest(
        ProjectStageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException(
                "عنوان مرحله الزامی است.");

        if (request.Title.Trim().Length > 200)
            throw new InvalidOperationException(
                "عنوان مرحله نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.");

        if (request.SortOrder < 0)
            throw new InvalidOperationException(
                "ترتیب مرحله نمی‌تواند منفی باشد.");

        if (request.PlannedStartDate.HasValue &&
            request.PlannedEndDate.HasValue &&
            request.PlannedEndDate < request.PlannedStartDate)
        {
            throw new InvalidOperationException(
                "تاریخ پایان برنامه‌ریزی‌شده نمی‌تواند قبل از تاریخ شروع باشد.");
        }
    }
}
