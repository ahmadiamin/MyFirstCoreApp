using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceReturnReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReferenceInvoiceItemId",
                table: "InvoiceItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_ReferenceInvoiceItemId",
                table: "InvoiceItems",
                column: "ReferenceInvoiceItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_InvoiceItems_ReferenceInvoiceItemId",
                table: "InvoiceItems",
                column: "ReferenceInvoiceItemId",
                principalTable: "InvoiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_InvoiceItems_ReferenceInvoiceItemId",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_ReferenceInvoiceItemId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "ReferenceInvoiceItemId",
                table: "InvoiceItems");
        }
    }
}
