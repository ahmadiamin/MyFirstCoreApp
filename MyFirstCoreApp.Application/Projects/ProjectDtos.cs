using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Projects;

public sealed class ProjectListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }

    public string? ContactName { get; set; }
    public string? ProjectManagerName { get; set; }

    public ProjectStatus Status { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }

    public int StageCount { get; set; }
    public int CompletedStageCount { get; set; }
}

public sealed class ProjectDetailsDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }

    public int? ContactId { get; set; }
    public string? ContactName { get; set; }

    public int? ProjectManagerUserTenantId { get; set; }
    public string? ProjectManagerName { get; set; }

    public ProjectStatus Status { get; set; }

    public string? Address { get; set; }
    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }

    public string? HoldReason { get; set; }
    public string? CancellationReason { get; set; }

    public List<ProjectMemberDto> Members { get; set; } = new();
    public List<ProjectStageDto> Stages { get; set; } = new();
    public List<ProjectStatusHistoryDto> StatusHistories { get; set; } = new();
    public List<ProjectInvoiceDto> Invoices { get; set; } = new();
}

public sealed class ProjectInvoiceDto
{
    public int Id { get; set; }
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public InvoiceStatus Status { get; set; }
    public decimal AllocatedAmount { get; set; }
    public int ProjectLineCount { get; set; }
}

public sealed class ProjectStatusHistoryDto
{
    public int Id { get; set; }
    public ProjectStatus? PreviousStatus { get; set; }
    public ProjectStatus NewStatus { get; set; }
    public string? Description { get; set; }
    public string? ChangedByUserName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class ProjectMemberDto
{
    public int Id { get; set; }
    public int UserTenantId { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }

    public string? RoleTitle { get; set; }
    public string? Notes { get; set; }
}

public sealed class ProjectStageDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }

    public ProjectStageStatus Status { get; set; }

    public int? AssignedUserTenantId { get; set; }
    public string? AssignedUserName { get; set; }

    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? PlannedEndDate { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? WaitingReason { get; set; }
    public DateOnly? NextFollowUpDate { get; set; }

    public string? Notes { get; set; }

    public List<ProjectStageHistoryDto> Histories { get; set; } = new();
}

public sealed class ProjectStageHistoryDto
{
    public int Id { get; set; }

    public ProjectStageStatus? PreviousStatus { get; set; }
    public ProjectStageStatus NewStatus { get; set; }

    public string? Description { get; set; }
    public string? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }
}
