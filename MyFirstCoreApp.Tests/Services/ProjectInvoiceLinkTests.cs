using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Domain.Entities.Invoicing;
using MyFirstCoreApp.Domain.Entities.Inventory;
using MyFirstCoreApp.Domain.Entities.Projects;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Services;
using MyFirstCoreApp.Tests.TestSupport;
using Xunit;

namespace MyFirstCoreApp.Tests.Services;

public sealed class ProjectInvoiceLinkTests : IDisposable
{
    private const int TenantId = 1;
    private readonly TestDatabase _database = new();

    public ProjectInvoiceLinkTests()
    {
        _database.Tenant.TenantId = TenantId;
        _database.User.UserId = "project-test-user";
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task GetById_AllocatesInvoiceLinesToTheirEffectiveProjects()
    {
        int headerProjectId;
        int otherProjectId;

        await using (var context = _database.NewContext())
        {
            var unit = new Unit { TenantId = TenantId, Name = "عدد", Symbol = "عدد", IsActive = true };
            context.Units.Add(unit);
            await context.SaveChangesAsync();

            var product = new Product
            {
                TenantId = TenantId,
                Name = "کالای تست",
                UnitId = unit.Id,
                IsActive = true,
                TrackInventory = false
            };
            var headerProject = new Project { TenantId = TenantId, Title = "پروژه سربرگ" };
            var otherProject = new Project { TenantId = TenantId, Title = "پروژه ردیف" };
            context.AddRange(product, headerProject, otherProject);
            await context.SaveChangesAsync();

            headerProjectId = headerProject.Id;
            otherProjectId = otherProject.Id;

            context.Invoices.Add(new Invoice
            {
                TenantId = TenantId,
                Number = 1,
                Date = new DateOnly(2026, 11, 21),
                InvoiceType = InvoiceType.Purchase,
                Status = InvoiceStatus.Draft,
                ProjectId = headerProjectId,
                TotalAmount = 1_500,
                Items =
                [
                    new InvoiceItem
                    {
                        TenantId = TenantId,
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPrice = 1_000,
                        LineTotal = 1_000
                    },
                    new InvoiceItem
                    {
                        TenantId = TenantId,
                        ProductId = product.Id,
                        ProjectId = otherProjectId,
                        Quantity = 1,
                        UnitPrice = 500,
                        LineTotal = 500
                    }
                ]
            });
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.NewContext();
        var service = new ProjectService(readContext, _database.User);

        var header = await service.GetByIdAsync(headerProjectId, TenantId);
        var rowProject = await service.GetByIdAsync(otherProjectId, TenantId);

        Assert.NotNull(header);
        Assert.NotNull(rowProject);
        var headerInvoice = Assert.Single(header.Invoices);
        Assert.Equal(1_000, headerInvoice.AllocatedAmount);
        Assert.Equal(1, headerInvoice.ProjectLineCount);
        var rowProjectInvoice = Assert.Single(rowProject.Invoices);
        Assert.Equal(500, rowProjectInvoice.AllocatedAmount);
        Assert.Equal(1, rowProjectInvoice.ProjectLineCount);
    }

    [Fact]
    public async Task StageExecutionReportsRecordProgressAndStopFollowUpHistory()
    {
        int projectId;
        int stageId;
        await using (var context = _database.NewContext())
        {
            var project = new Project { TenantId = TenantId, Title = "پروژه گزارش اجرا", Status = ProjectStatus.Active };
            var stage = new ProjectStage { TenantId = TenantId, Project = project, Title = "نصب", SortOrder = 1 };
            context.AddRange(project, stage);
            await context.SaveChangesAsync();
            projectId = project.Id;
            stageId = stage.Id;
        }

        await using var serviceContext = _database.NewContext();
        var service = new ProjectService(serviceContext, _database.User);
        await service.AddStageReportAsync(new MyFirstCoreApp.Application.Projects.ProjectStageReportRequest
        {
            ProjectStageId = stageId,
            ReportDate = new DateOnly(2026, 11, 21),
            ProgressPercent = 40,
            WorkSummary = "نصب کابل‌کشی انجام شد",
            IsStopped = true,
            StopReason = "منتظر رسیدن تابلو برق",
            NextFollowUpDate = new DateOnly(2026, 11, 23),
            ReportedByUserId = "project-test-user"
        }, TenantId);

        var details = await service.GetByIdAsync(projectId, TenantId);
        Assert.NotNull(details);
        var reportedStage = Assert.Single(details.Stages);
        Assert.Equal(ProjectStageStatus.Waiting, reportedStage.Status);
        var report = Assert.Single(reportedStage.Reports);
        Assert.Equal(40, report.ProgressPercent);
        Assert.True(report.IsStopped);
        Assert.Equal(new DateOnly(2026, 11, 23), report.NextFollowUpDate);
        Assert.Single(reportedStage.Histories);

        await service.AddStageReportAsync(new MyFirstCoreApp.Application.Projects.ProjectStageReportRequest
        {
            ProjectStageId = stageId,
            ReportDate = new DateOnly(2026, 11, 24),
            ProgressPercent = 65,
            WorkSummary = "نصب ادامه پیدا کرد",
            IsStopped = false,
            ReportedByUserId = "project-test-user"
        }, TenantId);

        details = await service.GetByIdAsync(projectId, TenantId);
        Assert.NotNull(details);
        reportedStage = Assert.Single(details.Stages);
        Assert.Equal(ProjectStageStatus.InProgress, reportedStage.Status);
        Assert.Null(reportedStage.WaitingReason);
        Assert.Equal(65, reportedStage.Reports[0].ProgressPercent);
        Assert.Equal(2, reportedStage.Histories.Count);
    }
}
