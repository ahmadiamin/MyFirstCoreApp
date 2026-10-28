using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChequeSmsReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ChequeReminderDayBeforeEnabled",
                table: "TenantSmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChequeReminderDueDayEnabled",
                table: "TenantSmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ChequeReminderEnabled",
                table: "TenantSmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChequeReminderText",
                table: "TenantSmsSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ChequeReminderTime",
                table: "TenantSmsSettings",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(9, 0));

            migrationBuilder.CreateTable(
                name: "ChequeSmsReminderDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChequeId = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScheduledAtLocal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ChequeSmsReminderDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChequeSmsReminderDispatches_Cheques_ChequeId",
                        column: x => x.ChequeId,
                        principalTable: "Cheques",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChequeSmsReminderDispatches_ChequeId",
                table: "ChequeSmsReminderDispatches",
                column: "ChequeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChequeSmsReminderDispatches_Status_NextAttemptAtUtc",
                table: "ChequeSmsReminderDispatches",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChequeSmsReminderDispatches_TenantId_ChequeId_DueDate_Kind",
                table: "ChequeSmsReminderDispatches",
                columns: new[] { "TenantId", "ChequeId", "DueDate", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [ChequeSmsReminderDispatches]) OR " +
                "EXISTS (SELECT 1 FROM [TenantSmsSettings] WHERE [ChequeReminderEnabled] = 1 " +
                "OR [ChequeReminderDayBeforeEnabled] = 1 OR [ChequeReminderDueDayEnabled] = 1 " +
                "OR [ChequeReminderText] IS NOT NULL) " +
                "THROW 51000, 'Cheque reminder settings or dispatch records exist; export or clear them before reverting this migration.', 1;");

            migrationBuilder.DropTable(
                name: "ChequeSmsReminderDispatches");

            migrationBuilder.DropColumn(
                name: "ChequeReminderDayBeforeEnabled",
                table: "TenantSmsSettings");

            migrationBuilder.DropColumn(
                name: "ChequeReminderDueDayEnabled",
                table: "TenantSmsSettings");

            migrationBuilder.DropColumn(
                name: "ChequeReminderEnabled",
                table: "TenantSmsSettings");

            migrationBuilder.DropColumn(
                name: "ChequeReminderText",
                table: "TenantSmsSettings");

            migrationBuilder.DropColumn(
                name: "ChequeReminderTime",
                table: "TenantSmsSettings");
        }
    }
}
