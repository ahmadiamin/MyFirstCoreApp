using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoiceNumberAndTenantLegalDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "Tenants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierInvoiceNumber",
                table: "Invoices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_ContactId_SupplierInvoiceNumber",
                table: "Invoices",
                columns: new[] { "TenantId", "ContactId", "SupplierInvoiceNumber" },
                unique: true,
                filter: "[InvoiceType] = 3 AND [SupplierInvoiceNumber] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_ContactId_SupplierInvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceNumber",
                table: "Invoices");
        }
    }
}
