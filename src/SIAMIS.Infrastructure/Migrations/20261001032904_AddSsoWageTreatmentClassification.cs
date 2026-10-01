using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSsoWageTreatmentClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SsoWageTreatment",
                table: "PayrollComponents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "SsoWageTreatmentSnapshot",
                table: "EmployeePayrollLines",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000001"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000002"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000003"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000004"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000005"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000006"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000007"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000008"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000009"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000010"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000001"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000002"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000003"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000004"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000005"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000006"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000007"),
                column: "SsoWageTreatment",
                value: "Unknown");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollComponents_SsoWageTreatment",
                table: "PayrollComponents",
                sql: "[SsoWageTreatment] IN ('Unknown', 'Included', 'Excluded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_SsoWageTreatmentSnapshot",
                table: "EmployeePayrollLines",
                sql: "[SsoWageTreatmentSnapshot] IN ('Unknown', 'Included', 'Excluded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollComponents_SsoWageTreatment",
                table: "PayrollComponents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_SsoWageTreatmentSnapshot",
                table: "EmployeePayrollLines");

            migrationBuilder.DropColumn(
                name: "SsoWageTreatment",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "SsoWageTreatmentSnapshot",
                table: "EmployeePayrollLines");
        }
    }
}
