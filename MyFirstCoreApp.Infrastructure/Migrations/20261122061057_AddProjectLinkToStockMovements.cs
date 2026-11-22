using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectLinkToStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "StockMovements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProjectId",
                table: "StockMovements",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId_ProjectId_MovementType_Date",
                table: "StockMovements",
                columns: new[] { "TenantId", "ProjectId", "MovementType", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Projects_ProjectId",
                table: "StockMovements",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Projects_ProjectId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ProjectId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TenantId_ProjectId_MovementType_Date",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "StockMovements");
        }
    }
}
