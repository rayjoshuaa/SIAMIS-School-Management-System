using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPitIncomeClassificationAndTaxTreatment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PitIncomeTreatment",
                table: "PayrollComponents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "EmploymentTaxTreatment",
                table: "EmployeeTaxDeclarations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "ResidencyStatus",
                table: "EmployeeTaxDeclarations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "PitIncomeTreatmentSnapshot",
                table: "EmployeePayrollLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000001"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000002"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000003"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000004"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000005"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000006"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000007"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000008"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000009"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000010"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000001"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000002"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000003"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000004"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000005"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000006"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000007"),
                column: "PitIncomeTreatment",
                value: "Unknown");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollComponents_PitIncomeTreatment",
                table: "PayrollComponents",
                sql: "[PitIncomeTreatment] IN ('Unknown', 'Included', 'Excluded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeTaxDeclarations_EmploymentTaxTreatment",
                table: "EmployeeTaxDeclarations",
                sql: "[EmploymentTaxTreatment] IN ('Unknown','StandardSection40_1','RequiresReview')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeTaxDeclarations_ResidencyStatus",
                table: "EmployeeTaxDeclarations",
                sql: "[ResidencyStatus] IN ('Unknown','Resident','NonResident')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_PitIncomeTreatmentSnapshot",
                table: "EmployeePayrollLines",
                sql: "[PitIncomeTreatmentSnapshot] IN ('Unknown', 'Included', 'Excluded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollComponents_PitIncomeTreatment",
                table: "PayrollComponents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeTaxDeclarations_EmploymentTaxTreatment",
                table: "EmployeeTaxDeclarations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeTaxDeclarations_ResidencyStatus",
                table: "EmployeeTaxDeclarations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_PitIncomeTreatmentSnapshot",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "PitIncomeTreatment",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "EmploymentTaxTreatment",
                table: "EmployeeTaxDeclarations");

            migrationBuilder.DropColumn(
                name: "ResidencyStatus",
                table: "EmployeeTaxDeclarations");

            migrationBuilder.DropColumn(
                name: "PitIncomeTreatmentSnapshot",
                table: "EmployeePayrollLines");
        }
    }
}
