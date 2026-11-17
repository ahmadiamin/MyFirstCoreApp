using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBrandingAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LogoAttachmentId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignatureAttachmentId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StampAttachmentId",
                table: "Tenants",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_LogoAttachmentId",
                table: "Tenants",
                column: "LogoAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SignatureAttachmentId",
                table: "Tenants",
                column: "SignatureAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_StampAttachmentId",
                table: "Tenants",
                column: "StampAttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Attachments_LogoAttachmentId",
                table: "Tenants",
                column: "LogoAttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Attachments_SignatureAttachmentId",
                table: "Tenants",
                column: "SignatureAttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Attachments_StampAttachmentId",
                table: "Tenants",
                column: "StampAttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Attachments_LogoAttachmentId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Attachments_SignatureAttachmentId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Attachments_StampAttachmentId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_LogoAttachmentId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_SignatureAttachmentId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_StampAttachmentId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LogoAttachmentId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SignatureAttachmentId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StampAttachmentId",
                table: "Tenants");
        }
    }
}
