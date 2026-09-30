using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePayrollLineAuditSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [EmployeePayrollLines]) THROW 51000, 'EmployeePayrollLines contains legacy rows without structured source provenance. Review and backfill them before applying AddEmployeePayrollLineAuditSnapshot.', 1;");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationMode",
                table: "EmployeePayrollLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                table: "EmployeePayrollLines",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BaseType",
                table: "EmployeePayrollLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationMethodSnapshot",
                table: "EmployeePayrollLines",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculationRate",
                table: "EmployeePayrollLines",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumBase",
                table: "EmployeePayrollLines",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumBase",
                table: "EmployeePayrollLines",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleCode",
                table: "EmployeePayrollLines",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuleName",
                table: "EmployeePayrollLines",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "EmployeePayrollLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "EmployeePayrollLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SourceType",
                table: "EmployeePayrollLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_SourceTypeAndId",
                table: "EmployeePayrollLines",
                sql: "([SourceType] = 'BasicSalary' AND [SourceId] IS NULL) OR ([SourceType] = 'Manual' AND [SourceId] IS NULL) OR ([SourceType] IN ('Assignment', 'PayrollRule') AND [SourceId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_SourceTypeAndId",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "ApplicationMode",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "BaseType",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "CalculationMethodSnapshot",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "CalculationRate",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "MaximumBase",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "MinimumBase",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "RuleCode",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "RuleName",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "EmployeePayrollLines");
        }
    }
}
