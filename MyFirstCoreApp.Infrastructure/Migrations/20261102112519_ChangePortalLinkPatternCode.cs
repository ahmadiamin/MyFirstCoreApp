using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangePortalLinkPatternCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortalLinkText",
                table: "TenantSmsSettings");

            migrationBuilder.AddColumn<string>(
                name: "PortalLinkPatternCode",
                table: "TenantSmsSettings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortalLinkPatternCode",
                table: "TenantSmsSettings");

            migrationBuilder.AddColumn<string>(
                name: "PortalLinkText",
                table: "TenantSmsSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
