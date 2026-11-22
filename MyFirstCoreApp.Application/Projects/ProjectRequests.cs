using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Projects;

/// <summary>
/// اطلاعات پایه برای ایجاد یا ویرایش پروژه.
/// </summary>
public sealed class ProjectRequest
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }

    public int? ContactId { get; set; }
    public int? ProjectManagerUserTenantId { get; set; }

    public string? Address { get; set; }
    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
}

/// <summary>
/// افزودن یا ویرایش عضو پروژه.
/// </summary>
public sealed class ProjectMemberRequest
{
    public int ProjectId { get; set; }
    public int UserTenantId { get; set; }

    public string? RoleTitle { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// ایجاد یا ویرایش مرحله پروژه.
/// </summary>
public sealed class ProjectStageRequest
{
    public int Id { get; set; }
    public int ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsRequired { get; set; } = true;

    public int? AssignedUserTenantId { get; set; }

    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// تغییر وضعیت مرحله پروژه.
/// </summary>
public sealed class ProjectStageStatusChangeRequest
{
    public int ProjectStageId { get; set; }

    public ProjectStageStatus NewStatus { get; set; }

    public string? Description { get; set; }
    public string? WaitingReason { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
}

public sealed class ProjectStageReportRequest
{
    public int ProjectStageId { get; set; }
    public DateOnly ReportDate { get; set; }
    public int ProgressPercent { get; set; }
    public string WorkSummary { get; set; } = string.Empty;
    public bool IsStopped { get; set; }
    public string? StopReason { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }
    public string? ReportedByUserId { get; set; }
}

/// <summary>
/// تغییر وضعیت کلی پروژه.
/// </summary>
public sealed class ProjectStatusChangeRequest
{
    public int ProjectId { get; set; }

    public ProjectStatus NewStatus { get; set; }

    public string? HoldReason { get; set; }
    public string? CancellationReason { get; set; }
}
