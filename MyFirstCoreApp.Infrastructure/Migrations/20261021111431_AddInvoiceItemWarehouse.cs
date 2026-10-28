using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceItemWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "InvoiceItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_WarehouseId",
                table: "InvoiceItems",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceItems_Warehouses_WarehouseId",
                table: "InvoiceItems",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [InvoiceItems] WHERE [WarehouseId] IS NOT NULL) " +
                "THROW 51000, 'Warehouse selections exist; back up this data before reverting this migration.', 1;");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceItems_Warehouses_WarehouseId",
                table: "InvoiceItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceItems_WarehouseId",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "InvoiceItems");

        }
    }
}
