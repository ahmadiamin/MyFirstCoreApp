using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerResponse",
                table: "Invoices",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSentDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowUpDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionCount",
                table: "Invoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentDate",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
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
                    table.PrimaryKey("PK_InvoiceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceHistories_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ApprovedDate",
                table: "Invoices",
                column: "ApprovedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_NextFollowUpDate",
                table: "Invoices",
                column: "NextFollowUpDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SentDate",
                table: "Invoices",
                column: "SentDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHistories_InvoiceId",
                table: "InvoiceHistories",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceHistories_TenantId_Date",
                table: "InvoiceHistories",
                columns: new[] { "TenantId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceHistories");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ApprovedDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_NextFollowUpDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_SentDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ApprovedDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerResponse",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastSentDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "NextFollowUpDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RejectedDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RevisionCount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SentDate",
                table: "Invoices");
        }
    }
}
