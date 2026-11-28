using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFirstCoreApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "Transactions",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Transactions");
        }
    }
}
