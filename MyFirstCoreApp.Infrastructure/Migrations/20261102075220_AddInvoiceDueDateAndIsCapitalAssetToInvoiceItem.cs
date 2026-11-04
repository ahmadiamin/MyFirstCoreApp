using Microsoft.EntityFrameworkCore.Migrations;
using MyFirstCoreApp.Infrastructure.Persistence.Sql;
using System;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDueDateAndIsCapitalAssetToInvoiceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCapitalAsset",
                table: "InvoiceItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // پروسیجرها به ستون DueDate وابسته‌اند؛ باید بعد از ساخت ستون دوباره نصب شوند
            migrationBuilder.Sql(ReportSqlScripts.Read("usp_Report_Invoices"));
            migrationBuilder.Sql(ReportSqlScripts.Read("usp_Report_ContactBalances"));
            migrationBuilder.Sql(ReportSqlScripts.Read("usp_Dashboard_Summary"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsCapitalAsset",
                table: "InvoiceItems");
        }
    }
}
