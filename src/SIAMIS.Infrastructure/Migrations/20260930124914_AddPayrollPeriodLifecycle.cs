using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollPeriodLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "PayrollPeriods",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "PayrollPeriods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "PayrollPeriods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "PayrollPeriods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollPeriods_Status",
                table: "PayrollPeriods",
                sql: "[Status] IN ('Open', 'Processing', 'Closed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollPeriods_Status",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "PayrollPeriods");
        }
    }
}
