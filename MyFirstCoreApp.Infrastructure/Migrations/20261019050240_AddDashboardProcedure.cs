using Microsoft.EntityFrameworkCore.Migrations;
using MyFirstCoreApp.Infrastructure.Persistence.Sql;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReportSqlScripts.Read("usp_Dashboard_Summary"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_Dashboard_Summary;");
        }
    }
}
