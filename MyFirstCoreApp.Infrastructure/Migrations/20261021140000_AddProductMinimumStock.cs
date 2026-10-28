using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations;

public partial class AddProductMinimumStock : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "MinimumStock",
            table: "Products",
            type: "decimal(18,3)",
            precision: 18,
            scale: 3,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF EXISTS (SELECT 1 FROM [Products] WHERE [MinimumStock] IS NOT NULL) " +
            "THROW 51000, 'Minimum stock settings exist; back up this data before reverting this migration.', 1;");

        migrationBuilder.DropColumn(name: "MinimumStock", table: "Products");
    }
}
