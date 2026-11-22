using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectStageExecutionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectStageReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectStageId = table.Column<int>(type: "int", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false),
                    WorkSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsStopped = table.Column<bool>(type: "bit", nullable: false),
                    StopReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NextFollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStageReports", x => x.Id);
                    table.CheckConstraint("CK_ProjectStageReports_ProgressPercent", "[ProgressPercent] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ProjectStageReports_ProjectStages_ProjectStageId",
                        column: x => x.ProjectStageId,
                        principalTable: "ProjectStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageReports_ProjectStageId",
                table: "ProjectStageReports",
                column: "ProjectStageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStageReports_TenantId_ProjectStageId_ReportDate",
                table: "ProjectStageReports",
                columns: new[] { "TenantId", "ProjectStageId", "ReportDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectStageReports");
        }
    }
}
