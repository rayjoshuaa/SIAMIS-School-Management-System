using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeavePaidUnpaidAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_Authoritative",
                table: "EmployeeLeave");

            migrationBuilder.AddColumn<int>(
                name: "PaidMinutes",
                table: "EmployeeLeaveAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnpaidMinutes",
                table: "EmployeeLeaveAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveAllocation_Payment",
                table: "EmployeeLeaveAllocations",
                sql: "([PaidMinutes] IS NULL AND [UnpaidMinutes] IS NULL) OR ([PaidMinutes] IS NOT NULL AND [UnpaidMinutes] IS NOT NULL AND [PaidMinutes] >= 0 AND [UnpaidMinutes] >= 0 AND CONVERT(bigint,[PaidMinutes]) + [UnpaidMinutes] = [ChargeableMinutes])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_Authoritative",
                table: "EmployeeLeave",
                sql: "[RequestMode] IS NULL OR ([RequestMode] IN ('FullDay','Timed') AND [NoticeCategory] IS NOT NULL AND [NoticeCategory] IN ('Foreseeable','SuddenIllness') AND [BalanceTracked] IS NOT NULL AND [RequestedAt] IS NOT NULL AND [ChargeableMinutes] IS NOT NULL AND [ChargeableMinutes] > 0 AND [CalculationSnapshotVersion] IN (1,2) AND [CalculationSnapshotJson] IS NOT NULL AND (([RequestMode] = 'FullDay' AND [RequestedStartTime] IS NULL AND [RequestedEndTime] IS NULL) OR ([RequestMode] = 'Timed' AND [RequestedStartTime] IS NOT NULL AND [RequestedEndTime] IS NOT NULL)))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveAllocation_Payment",
                table: "EmployeeLeaveAllocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_Authoritative",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "PaidMinutes",
                table: "EmployeeLeaveAllocations");

            migrationBuilder.DropColumn(
                name: "UnpaidMinutes",
                table: "EmployeeLeaveAllocations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_Authoritative",
                table: "EmployeeLeave",
                sql: "[RequestMode] IS NULL OR ([RequestMode] IN ('FullDay','Timed') AND [NoticeCategory] IS NOT NULL AND [NoticeCategory] IN ('Foreseeable','SuddenIllness') AND [BalanceTracked] IS NOT NULL AND [RequestedAt] IS NOT NULL AND [ChargeableMinutes] IS NOT NULL AND [ChargeableMinutes] > 0 AND [CalculationSnapshotVersion] = 1 AND [CalculationSnapshotJson] IS NOT NULL AND (([RequestMode] = 'FullDay' AND [RequestedStartTime] IS NULL AND [RequestedEndTime] IS NULL) OR ([RequestMode] = 'Timed' AND [RequestedStartTime] IS NOT NULL AND [RequestedEndTime] IS NOT NULL)))");
        }
    }
}
