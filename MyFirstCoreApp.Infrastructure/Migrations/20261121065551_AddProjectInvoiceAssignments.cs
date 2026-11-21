using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectInvoiceAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "InvoiceItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ProjectId",
                table: "Invoices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_ProjectId",
                table: "Invoices",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_ProjectId",
                table: "InvoiceItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_TenantId_ProjectId",
                table: "InvoiceItems",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_Projects_ProjectId",
                table: "InvoiceItems",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Projects_ProjectId",
                table: "Invoices",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_Projects_ProjectId",
                table: "InvoiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Projects_ProjectId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ProjectId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_ProjectId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_ProjectId",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_TenantId_ProjectId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "InvoiceItems");
        }
    }
}
