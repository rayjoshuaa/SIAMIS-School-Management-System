using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBasicSalaryEntitlementSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BasicSalaryProrationMethod",
                table: "PayrollSettings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ThirtyDay");

            migrationBuilder.AddColumn<string>(
                name: "BasicSalaryCalculationSnapshotJson",
                table: "EmployeePayrollLines",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollSettings_BasicSalaryProrationMethod",
                table: "PayrollSettings",
                sql: "[BasicSalaryProrationMethod] = 'ThirtyDay'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_BasicSalarySnapshotJson",
                table: "EmployeePayrollLines",
                sql: "[BasicSalaryCalculationSnapshotJson] IS NULL OR ([SourceType] = 'BasicSalary' AND ISJSON([BasicSalaryCalculationSnapshotJson]) = 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollSettings_BasicSalaryProrationMethod",
                table: "PayrollSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_BasicSalarySnapshotJson",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "BasicSalaryProrationMethod",
                table: "PayrollSettings");

            migrationBuilder.DropColumn(
                name: "BasicSalaryCalculationSnapshotJson",
                table: "EmployeePayrollLines");
        }
    }
}
