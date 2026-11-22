using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyAssetRegisterAndAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AcquisitionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AcquisitionCost = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CompanyAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyAssets_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyAssetAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyAssetId = table.Column<int>(type: "int", nullable: false),
                    UserTenantId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    AssignedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReturnedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReturnNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_CompanyAssetAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyAssetAssignments_CompanyAssets_CompanyAssetId",
                        column: x => x.CompanyAssetId,
                        principalTable: "CompanyAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyAssetAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyAssetAssignments_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssetAssignments_CompanyAssetId",
                table: "CompanyAssetAssignments",
                column: "CompanyAssetId",
                unique: true,
                filter: "[ReturnedDate] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssetAssignments_ProjectId",
                table: "CompanyAssetAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssetAssignments_TenantId_CompanyAssetId_AssignedDate",
                table: "CompanyAssetAssignments",
                columns: new[] { "TenantId", "CompanyAssetId", "AssignedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssetAssignments_UserTenantId",
                table: "CompanyAssetAssignments",
                column: "UserTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssets_ProductId",
                table: "CompanyAssets",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssets_TenantId_Number",
                table: "CompanyAssets",
                columns: new[] { "TenantId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssets_TenantId_ProductId",
                table: "CompanyAssets",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyAssets_TenantId_SerialNumber",
                table: "CompanyAssets",
                columns: new[] { "TenantId", "SerialNumber" },
                unique: true,
                filter: "[SerialNumber] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyAssetAssignments");

            migrationBuilder.DropTable(
                name: "CompanyAssets");
        }
    }
}
