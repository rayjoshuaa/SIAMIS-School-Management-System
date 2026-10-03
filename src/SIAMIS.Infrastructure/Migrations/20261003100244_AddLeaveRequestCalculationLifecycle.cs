using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveRequestCalculationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BalanceTracked",
                table: "EmployeeLeave",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationRemarks",
                table: "EmployeeLeave",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "EmployeeLeave",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoticeCategory",
                table: "EmployeeLeave",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestMode",
                table: "EmployeeLeave",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAt",
                table: "EmployeeLeave",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewRemarks",
                table: "EmployeeLeave",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "EmployeeLeave",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeLeaveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveYear = table.Column<int>(type: "int", nullable: false),
                    ChargeableMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveAllocations", x => x.Id);
                    table.CheckConstraint("CK_LeaveAllocation_Minutes", "[ChargeableMinutes] > 0");
                    table.CheckConstraint("CK_LeaveAllocation_Year", "[LeaveYear] BETWEEN 1 AND 9999");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveAllocations_EmployeeLeave_EmployeeLeaveId",
                        column: x => x.EmployeeLeaveId,
                        principalTable: "EmployeeLeave",
                        principalColumn: "LeaveId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeave_EmployeeId_Status_StartDate",
                table: "EmployeeLeave",
                columns: new[] { "EmployeeId", "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeave_Status_RequestedAt",
                table: "EmployeeLeave",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_Authoritative",
                table: "EmployeeLeave",
                sql: "[RequestMode] IS NULL OR ([RequestMode] IN ('FullDay','Timed') AND [NoticeCategory] IS NOT NULL AND [NoticeCategory] IN ('Foreseeable','SuddenIllness') AND [BalanceTracked] IS NOT NULL AND [RequestedAt] IS NOT NULL AND [ChargeableMinutes] IS NOT NULL AND [ChargeableMinutes] > 0 AND [CalculationSnapshotVersion] = 1 AND [CalculationSnapshotJson] IS NOT NULL AND (([RequestMode] = 'FullDay' AND [RequestedStartTime] IS NULL AND [RequestedEndTime] IS NULL) OR ([RequestMode] = 'Timed' AND [RequestedStartTime] IS NOT NULL AND [RequestedEndTime] IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_Lifecycle",
                table: "EmployeeLeave",
                sql: "[RequestMode] IS NULL OR (([Status] = 'Pending' AND [ReviewedAt] IS NULL AND [CancelledAt] IS NULL) OR ([Status] IN ('Approved','Rejected') AND [ReviewedAt] IS NOT NULL AND [CancelledAt] IS NULL) OR ([Status] = 'Cancelled' AND [CancelledAt] IS NOT NULL AND ([ReviewedAt] IS NULL OR ([CancellationRemarks] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationRemarks]))) > 0))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_Status",
                table: "EmployeeLeave",
                sql: "[Status] IN ('Pending','Approved','Rejected','Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveAllocations_EmployeeLeaveId_LeaveYear",
                table: "EmployeeLeaveAllocations",
                columns: new[] { "EmployeeLeaveId", "LeaveYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeLeaveAllocations");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeLeave_EmployeeId_Status_StartDate",
                table: "EmployeeLeave");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeLeave_Status_RequestedAt",
                table: "EmployeeLeave");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_Authoritative",
                table: "EmployeeLeave");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_Lifecycle",
                table: "EmployeeLeave");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_Status",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "BalanceTracked",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "CancellationRemarks",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "NoticeCategory",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "RequestMode",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "RequestedAt",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "ReviewRemarks",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "EmployeeLeave");
        }
    }
}
