using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPitPaymentTreatmentClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PitPaymentTreatment",
                table: "PayrollComponents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "PitPaymentTreatmentSnapshot",
                table: "EmployeePayrollLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000001"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000002"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000003"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000004"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000005"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000006"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000007"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000008"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000009"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000010"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000001"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000002"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000003"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000004"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000005"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000006"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000007"),
                column: "PitPaymentTreatment",
                value: "Unknown");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollComponents_PitPaymentTreatment",
                table: "PayrollComponents",
                sql: "[PitPaymentTreatment] IN ('Unknown', 'Regular', 'Special')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_PitPaymentTreatmentSnapshot",
                table: "EmployeePayrollLines",
                sql: "[PitPaymentTreatmentSnapshot] IN ('Unknown', 'Regular', 'Special')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollComponents_PitPaymentTreatment",
                table: "PayrollComponents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_PitPaymentTreatmentSnapshot",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "PitPaymentTreatment",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "PitPaymentTreatmentSnapshot",
                table: "EmployeePayrollLines");
        }
    }
}
