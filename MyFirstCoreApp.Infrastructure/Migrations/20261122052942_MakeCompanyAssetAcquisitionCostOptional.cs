using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCompanyAssetAcquisitionCostOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "AcquisitionCost",
                table: "CompanyAssets",
                type: "decimal(18,0)",
                precision: 18,
                scale: 0,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "AcquisitionCost",
                table: "CompanyAssets",
                type: "decimal(18,0)",
                precision: 18,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,0)",
                oldPrecision: 18,
                oldScale: 0,
                oldNullable: true);
        }
    }
}
