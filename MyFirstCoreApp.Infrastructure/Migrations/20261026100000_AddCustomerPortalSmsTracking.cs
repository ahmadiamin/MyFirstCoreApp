using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations;

public partial class AddCustomerPortalSmsTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LastSmsRequestId",
            table: "CustomerPortalShares",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastSmsSentAtUtc",
            table: "CustomerPortalShares",
            type: "datetime2",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LastSmsRequestId", table: "CustomerPortalShares");
        migrationBuilder.DropColumn(name: "LastSmsSentAtUtc", table: "CustomerPortalShares");
    }
}
