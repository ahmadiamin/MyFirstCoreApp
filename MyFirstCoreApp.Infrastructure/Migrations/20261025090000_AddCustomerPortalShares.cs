using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations;

public partial class AddCustomerPortalShares : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CustomerPortalShares",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ContactId = table.Column<int>(type: "int", nullable: false),
                TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CanViewLedger = table.Column<bool>(type: "bit", nullable: false),
                CanViewSalesInvoices = table.Column<bool>(type: "bit", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                TenantId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerPortalShares", x => x.Id);
                table.ForeignKey(
                    name: "FK_CustomerPortalShares_Contacts_ContactId",
                    column: x => x.ContactId,
                    principalTable: "Contacts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomerPortalShares_ContactId",
            table: "CustomerPortalShares",
            column: "ContactId");
        migrationBuilder.CreateIndex(
            name: "IX_CustomerPortalShares_TenantId_ContactId_RevokedAtUtc",
            table: "CustomerPortalShares",
            columns: new[] { "TenantId", "ContactId", "RevokedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_CustomerPortalShares_TokenHash",
            table: "CustomerPortalShares",
            column: "TokenHash",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF EXISTS (SELECT 1 FROM [CustomerPortalShares]) " +
            "THROW 51000, 'Customer portal links exist; revoke or back up this data before reverting this migration.', 1;");
        migrationBuilder.DropTable(name: "CustomerPortalShares");
    }
}
