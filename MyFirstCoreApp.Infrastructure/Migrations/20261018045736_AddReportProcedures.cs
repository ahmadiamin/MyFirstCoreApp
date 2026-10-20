using Microsoft.EntityFrameworkCore.Migrations;
using MyFirstCoreApp.Infrastructure.Persistence.Sql;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var name in ReportSqlScripts.ProcedureNames)
                migrationBuilder.Sql(ReportSqlScripts.Read(name));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var name in ReportSqlScripts.ProcedureNames)
                migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS dbo.{name};");
        }
    }
}
