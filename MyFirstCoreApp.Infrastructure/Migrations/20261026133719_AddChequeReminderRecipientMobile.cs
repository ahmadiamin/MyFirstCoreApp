using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChequeReminderRecipientMobile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChequeReminderMobile",
                table: "TenantSmsSettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientMobile",
                table: "ChequeSmsReminderDispatches",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [TenantSmsSettings] WHERE [ChequeReminderMobile] IS NOT NULL) OR " +
                "EXISTS (SELECT 1 FROM [ChequeSmsReminderDispatches] WHERE [RecipientMobile] IS NOT NULL) " +
                "THROW 51000, 'Cheque reminder recipient mobile numbers exist; export or clear them before reverting this migration.', 1;");

            migrationBuilder.DropColumn(
                name: "RecipientMobile",
                table: "ChequeSmsReminderDispatches");

            migrationBuilder.DropColumn(
                name: "ChequeReminderMobile",
                table: "TenantSmsSettings");
        }
    }
}
