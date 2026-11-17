using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactPersonsAndNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactPersons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPersons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactPersons_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactPersonNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactPersonId = table.Column<int>(type: "int", nullable: false),
                    NumberType = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPersonNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactPersonNumbers_ContactPersons_ContactPersonId",
                        column: x => x.ContactPersonId,
                        principalTable: "ContactPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonNumbers_ContactPersonId",
                table: "ContactPersonNumbers",
                column: "ContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersonNumbers_TenantId_ContactPersonId_NumberType",
                table: "ContactPersonNumbers",
                columns: new[] { "TenantId", "ContactPersonId", "NumberType" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_ContactId",
                table: "ContactPersons",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactPersons_TenantId_ContactId_IsActive",
                table: "ContactPersons",
                columns: new[] { "TenantId", "ContactId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactPersonNumbers");

            migrationBuilder.DropTable(
                name: "ContactPersons");
        }
    }
}
