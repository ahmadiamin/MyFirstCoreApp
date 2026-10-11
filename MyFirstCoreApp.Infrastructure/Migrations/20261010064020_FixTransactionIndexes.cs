using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixTransactionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId_Number",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_InvoiceType",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_Number",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId_Number",
                table: "Transactions",
                columns: new[] { "TenantId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_InvoiceType_Number",
                table: "Invoices",
                columns: new[] { "TenantId", "InvoiceType", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId_Number",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_InvoiceType_Number",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId_Number",
                table: "Transactions",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceType",
                table: "Invoices",
                column: "InvoiceType");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_Number",
                table: "Invoices",
                columns: new[] { "TenantId", "Number" });
        }
    }
}
