
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Areas.Projects.ViewModels.Project;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Projects.Controllers;

[Area("Projects"), Authorize(Policy = "Project.Report.View")]
public sealed class ProjectReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly TimeProvider _timeProvider;

    public ProjectReportsController(
        ApplicationDbContext db,
        ICurrentTenant tenant,
        TimeProvider timeProvider)
    {
        _db = db;
        _tenant = tenant;
        _timeProvider = timeProvider;
    }

    public async Task<IActionResult> Index()
    {
        if (!_tenant.TenantId.HasValue)
            return Forbid();

        var tenantId = _tenant.TenantId.Value;
        var today = _timeProvider.Today();

        // پروژه‌های مستأجر جاری
        var projects = await _db.Projects
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new ProjectFinancialReportRow
            {
                ProjectId = x.Id,
                Title = x.Title,
                Status = x.Status
            })
            .ToListAsync();

        // دسترسی سریع به پروژه‌ها برای تجمیع مبالغ
        var projectLookup = projects.ToDictionary(x => x.ProjectId);

        // فقط ردیف فاکتورهای قطعی و متعلق به مستأجر جاری محاسبه می‌شوند.
        // در صورت نبود پروژه در ردیف، پروژه سربرگ فاکتور ملاک است.
        var invoiceLines = await _db.InvoiceItems
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Invoice.TenantId == tenantId &&
                x.Invoice.Status == InvoiceStatus.Posted &&
                (x.ProjectId.HasValue || x.Invoice.ProjectId.HasValue) &&
                (
                    !x.ProjectId.HasValue ||
                    (x.Project != null && x.Project.TenantId == tenantId)
                ) &&
                (
                    x.ProjectId.HasValue ||
                    (x.Invoice.Project != null && x.Invoice.Project.TenantId == tenantId)
                ))
            .Select(x => new
            {
                ProjectId = x.ProjectId ?? x.Invoice.ProjectId!.Value,
                x.Invoice.InvoiceType,
                x.LineTotal
            })
            .ToListAsync();

        foreach (var line in invoiceLines)
        {
            if (!projectLookup.TryGetValue(line.ProjectId, out var project))
                continue;

            switch (line.InvoiceType)
            {
                case InvoiceType.Sale:
                    project.Sales += line.LineTotal;
                    break;

                case InvoiceType.SalesReturn:
                    project.Sales -= line.LineTotal;
                    break;

                case InvoiceType.Purchase:
                    project.Purchases += line.LineTotal;
                    break;

                case InvoiceType.PurchaseReturn:
                    project.Purchases -= line.LineTotal;
                    break;
            }
        }

        // هزینه‌های قطعی پروژه‌های مستأجر جاری
        var expenses = await _db.ProjectExpenses
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.ProjectId.HasValue &&
                x.Project != null &&
                x.Project.TenantId == tenantId &&
                x.IsPosted)
            .GroupBy(x => x.ProjectId!.Value)
            .Select(g => new
            {
                ProjectId = g.Key,
                Amount = g.Sum(x => x.Amount)
            })
            .ToListAsync();

        foreach (var expense in expenses)
        {
            if (projectLookup.TryGetValue(expense.ProjectId, out var project))
                project.Expenses = expense.Amount;
        }

        // مراحل عقب‌افتاده پروژه‌های مستأجر جاری
        var delayedStages = await _db.ProjectStages
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Project.TenantId == tenantId &&
                x.PlannedEndDate.HasValue &&
                x.PlannedEndDate.Value < today &&
                x.Status != ProjectStageStatus.Completed &&
                x.Status != ProjectStageStatus.Skipped &&
                x.Status != ProjectStageStatus.Cancelled)
            .Select(x => new ProjectStageDelayReportRow
            {
                ProjectId = x.ProjectId,
                ProjectTitle = x.Project.Title,
                StageTitle = x.Title,
                PlannedEndDate = x.PlannedEndDate!.Value,
                Status = x.Status,
                WaitingReason = x.WaitingReason
            })
            .OrderBy(x => x.PlannedEndDate)
            .ToListAsync();

        foreach (var stage in delayedStages)
        {
            stage.DelayDays = today.DayNumber - stage.PlannedEndDate.DayNumber;
        }

        // پروژه‌های متوقف‌شده
        var onHoldProjects = await _db.Projects
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Status == ProjectStatus.OnHold)
            .OrderBy(x => x.PlannedEndDate)
            .Select(x => new ProjectOnHoldReportRow
            {
                ProjectId = x.Id,
                Title = x.Title,
                HoldReason = x.HoldReason,
                PlannedEndDate = x.PlannedEndDate
            })
            .ToListAsync();

        // مراحل تخصیص‌یافته به کارکنان پروژه‌های مستأجر جاری
        var stageAssignments = await _db.ProjectStages
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Project.TenantId == tenantId &&
                x.AssignedUserTenantId.HasValue)
            .Select(x => new
            {
                x.AssignedUserTenantId,
                x.Status,
                x.PlannedEndDate
            })
            .ToListAsync();

        var userTenantIds = stageAssignments
            .Select(x => x.AssignedUserTenantId!.Value)
            .Distinct()
            .ToList();

        // دریافت مشخصات کارکنان عضو همین مستأجر
        var employees = await (
            from membership in _db.UserTenants.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.TenantId == tenantId &&
                  userTenantIds.Contains(membership.Id)
            select new
            {
                membership.Id,
                Name = (membership.FirstName ?? "") + " " +
                       (membership.LastName ?? ""),
                user.FullName
            })
            .ToListAsync();

        var employeeLookup = employees.ToDictionary(x => x.Id);

        // گزارش عملکرد کارکنان
        var employeeRows = stageAssignments
            .GroupBy(x => x.AssignedUserTenantId!.Value)
            .Select(group =>
            {
                employeeLookup.TryGetValue(group.Key, out var employee);

                return new ProjectEmployeePerformanceRow
                {
                    EmployeeName = string.IsNullOrWhiteSpace(employee?.Name)
                        ? employee?.FullName ?? "کاربر"
                        : employee.Name.Trim(),

                    AssignedStageCount = group.Count(),

                    CompletedStageCount = group.Count(x =>
                        x.Status == ProjectStageStatus.Completed),

                    WaitingStageCount = group.Count(x =>
                        x.Status == ProjectStageStatus.Waiting),

                    DelayedStageCount = group.Count(x =>
                        x.PlannedEndDate.HasValue &&
                        x.PlannedEndDate.Value < today &&
                        x.Status != ProjectStageStatus.Completed &&
                        x.Status != ProjectStageStatus.Skipped &&
                        x.Status != ProjectStageStatus.Cancelled)
                };
            })
            .OrderByDescending(x => x.CompletedStageCount)
            .ThenBy(x => x.EmployeeName)
            .ToList();

        // ساخت مدل نهایی گزارش
        var model = new ProjectManagementReportViewModel
        {
            AsOfDate = today,

            TotalSales = projects.Sum(x => x.Sales),
            TotalPurchases = projects.Sum(x => x.Purchases),
            TotalExpenses = projects.Sum(x => x.Expenses),

            ActiveProjectCount = projects.Count(x =>
                x.Status == ProjectStatus.Active),

            OnHoldProjectCount = projects.Count(x =>
                x.Status == ProjectStatus.OnHold),

            DelayedStageCount = delayedStages.Count,

            Projects = projects
                .OrderBy(x => x.Title)
                .ToList(),

            OnHoldProjects = onHoldProjects,
            DelayedStages = delayedStages,
            Employees = employeeRows
        };

        return View(model);
    }
}