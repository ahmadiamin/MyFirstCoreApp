using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Projects.ViewModels.Project;

public sealed class ProjectManagementReportViewModel
{
    public DateOnly AsOfDate { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetDifference => TotalSales - TotalPurchases - TotalExpenses;
    public int ActiveProjectCount { get; set; }
    public int OnHoldProjectCount { get; set; }
    public int DelayedStageCount { get; set; }
    public List<ProjectFinancialReportRow> Projects { get; set; } = [];
    public List<ProjectOnHoldReportRow> OnHoldProjects { get; set; } = [];
    public List<ProjectStageDelayReportRow> DelayedStages { get; set; } = [];
    public List<ProjectEmployeePerformanceRow> Employees { get; set; } = [];
}

public sealed class ProjectOnHoldReportRow
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? HoldReason { get; set; }
    public DateOnly? PlannedEndDate { get; set; }
}

public sealed class ProjectFinancialReportRow
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public decimal Sales { get; set; }
    public decimal Purchases { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetDifference => Sales - Purchases - Expenses;
}

public sealed class ProjectStageDelayReportRow
{
    public int ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string StageTitle { get; set; } = string.Empty;
    public DateOnly PlannedEndDate { get; set; }
    public int DelayDays { get; set; }
    public ProjectStageStatus Status { get; set; }
    public string? WaitingReason { get; set; }
}

public sealed class ProjectEmployeePerformanceRow
{
    public string EmployeeName { get; set; } = string.Empty;
    public int AssignedStageCount { get; set; }
    public int CompletedStageCount { get; set; }
    public int WaitingStageCount { get; set; }
    public int DelayedStageCount { get; set; }
}
