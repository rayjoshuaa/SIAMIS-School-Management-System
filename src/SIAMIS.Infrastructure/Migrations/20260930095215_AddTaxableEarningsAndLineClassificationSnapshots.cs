using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxableEarningsAndLineClassificationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxableEarnings",
                table: "EmployeePayrolls",
                type: "decimal(19,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ContributionSideSnapshot",
                table: "EmployeePayrollLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStatutorySnapshot",
                table: "EmployeePayrollLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxableSnapshot",
                table: "EmployeePayrollLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrolls_TaxableEarningsNonNegative",
                table: "EmployeePayrolls",
                sql: "[TaxableEarnings] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_ContributionSideSnapshot",
                table: "EmployeePayrollLines",
                sql: "[ContributionSideSnapshot] IS NULL OR [ContributionSideSnapshot] IN ('Employee', 'Employer', 'Both')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrolls_TaxableEarningsNonNegative",
                table: "EmployeePayrolls");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_ContributionSideSnapshot",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "TaxableEarnings",
                table: "EmployeePayrolls");

            migrationBuilder.DropColumn(
                name: "ContributionSideSnapshot",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "IsStatutorySnapshot",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "IsTaxableSnapshot",
                table: "EmployeePayrollLines");
        }
    }
}
