using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordRecoverySmsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PasswordRecoverySmsEnabled",
                table: "SystemSmsSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordRecoverySmsPatternCode",
                table: "SystemSmsSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordRecoverySmsEnabled",
                table: "SystemSmsSettings");

            migrationBuilder.DropColumn(
                name: "PasswordRecoverySmsPatternCode",
                table: "SystemSmsSettings");
        }
    }
}
