using Microsoft.EntityFrameworkCore.Migrations;
using MyFirstCoreApp.Infrastructure.Persistence.Sql;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactBalancesProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportSqlScripts.Read("usp_Report_ContactBalances"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Report_ContactBalances;");
        }
    }
}