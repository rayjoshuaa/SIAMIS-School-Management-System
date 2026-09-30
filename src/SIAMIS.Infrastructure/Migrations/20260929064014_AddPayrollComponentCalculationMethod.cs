using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollComponentCalculationMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalculationMethod",
                table: "PayrollComponents",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000001"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000002"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000003"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000004"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000005"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000006"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000007"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000008"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000009"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000010"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000001"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000002"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000003"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000004"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000005"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000006"),
                column: "CalculationMethod",
                value: "FixedAmount");

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000007"),
                column: "CalculationMethod",
                value: "FixedAmount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalculationMethod",
                table: "PayrollComponents");
        }
    }
}
