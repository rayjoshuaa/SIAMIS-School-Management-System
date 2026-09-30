using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollComponentClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContributionSide",
                table: "PayrollComponents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStatutory",
                table: "PayrollComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxable",
                table: "PayrollComponents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000001"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000002"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000003"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000004"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000005"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000006"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000007"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000008"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000009"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f0000000-0000-0000-0000-000000000010"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000001"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000002"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000003"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000004"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000005"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000006"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.UpdateData(
                table: "PayrollComponents",
                keyColumn: "Id",
                keyValue: new Guid("f1000000-0000-0000-0000-000000000007"),
                column: "ContributionSide",
                value: null);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollComponents_ContributionSide",
                table: "PayrollComponents",
                sql: "[ContributionSide] IS NULL OR [ContributionSide] IN ('Employee', 'Employer', 'Both')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollComponents_ContributionSide",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "ContributionSide",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "IsStatutory",
                table: "PayrollComponents");

            migrationBuilder.DropColumn(
                name: "IsTaxable",
                table: "PayrollComponents");
        }
    }
}
