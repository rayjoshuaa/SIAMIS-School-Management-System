using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveCalendarPolicyEntitlementFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalculationSnapshotJson",
                table: "EmployeeLeave",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalculationSnapshotVersion",
                table: "EmployeeLeave",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargeableMinutes",
                table: "EmployeeLeave",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "RequestedEndTime",
                table: "EmployeeLeave",
                type: "time(0)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "RequestedStartTime",
                table: "EmployeeLeave",
                type: "time(0)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveYear = table.Column<int>(type: "int", nullable: false),
                    EntitledMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveEntitlements", x => x.Id);
                    table.CheckConstraint("CK_Entitlement_Minutes", "[EntitledMinutes] >= 0");
                    table.CheckConstraint("CK_Entitlement_Year", "[LeaveYear] BETWEEN 1 AND 9999");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEntitlements_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEntitlements_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LeavePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BalanceTracked = table.Column<bool>(type: "bit", nullable: false),
                    ForeseeableNoticeHours = table.Column<int>(type: "int", nullable: true),
                    AllowsSuddenRequest = table.Column<bool>(type: "bit", nullable: false),
                    SupportingDocumentPolicy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CertificateAfterConsecutiveDays = table.Column<int>(type: "int", nullable: true),
                    CertificateOnMondayWorkingDate = table.Column<bool>(type: "bit", nullable: false),
                    CertificateOnFridayWorkingDate = table.Column<bool>(type: "bit", nullable: false),
                    SandwichParticipation = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePolicies", x => x.Id);
                    table.CheckConstraint("CK_LeavePolicy_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_LeavePolicy_Document", "([SupportingDocumentPolicy] = 'None' AND [DocumentTypeId] IS NULL AND [CertificateAfterConsecutiveDays] IS NULL AND [CertificateOnMondayWorkingDate] = 0 AND [CertificateOnFridayWorkingDate] = 0) OR ([SupportingDocumentPolicy] IN ('AlwaysRequired','Conditional') AND [DocumentTypeId] IS NOT NULL)");
                    table.CheckConstraint("CK_LeavePolicy_Notice", "[ForeseeableNoticeHours] IS NULL OR [ForeseeableNoticeHours] >= 0");
                    table.CheckConstraint("CK_LeavePolicy_Status", "([Status] = 'Draft' AND [PublishedAt] IS NULL) OR ([Status] = 'Published' AND [PublishedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_LeavePolicy_Threshold", "[CertificateAfterConsecutiveDays] IS NULL OR [CertificateAfterConsecutiveDays] >= 0");
                    table.ForeignKey(
                        name: "FK_LeavePolicies_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeavePolicies_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveEntitlementAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeLeaveEntitlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentMinutes = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveEntitlementAdjustments", x => x.Id);
                    table.CheckConstraint("CK_EntitlementAdjustment_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0 AND [AdjustmentMinutes] <> 0");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEntitlementAdjustments_EmployeeLeaveEntitlements_EmployeeLeaveEntitlementId",
                        column: x => x.EmployeeLeaveEntitlementId,
                        principalTable: "EmployeeLeaveEntitlements",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWorkCalendarAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWorkCalendarAssignments", x => x.Id);
                    table.CheckConstraint("CK_CalendarAssignment_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_EmployeeWorkCalendarAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeeWorkCalendarAssignments_WorkCalendars_WorkCalendarId",
                        column: x => x.WorkCalendarId,
                        principalTable: "WorkCalendars",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendarDateOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    OverrideType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendarDateOverrides", x => x.Id);
                    table.CheckConstraint("CK_DateOverride_Type", "[OverrideType] IN ('PublicHoliday','SchoolHoliday','RestDay','ExceptionalWorkingDay')");
                    table.ForeignKey(
                        name: "FK_WorkCalendarDateOverrides_WorkCalendars_WorkCalendarId",
                        column: x => x.WorkCalendarId,
                        principalTable: "WorkCalendars",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendarWeeklyIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkCalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendarWeeklyIntervals", x => x.Id);
                    table.CheckConstraint("CK_WeeklyInterval_Day", "[DayOfWeek] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_WeeklyInterval_Time", "[StartTime] < [EndTime] AND DATEPART(SECOND,[StartTime]) = 0 AND DATEPART(SECOND,[EndTime]) = 0");
                    table.ForeignKey(
                        name: "FK_WorkCalendarWeeklyIntervals_WorkCalendars_WorkCalendarId",
                        column: x => x.WorkCalendarId,
                        principalTable: "WorkCalendars",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkCalendarOverrideIntervals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkCalendarDateOverrideId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCalendarOverrideIntervals", x => x.Id);
                    table.CheckConstraint("CK_OverrideInterval_Time", "[StartTime] < [EndTime] AND DATEPART(SECOND,[StartTime]) = 0 AND DATEPART(SECOND,[EndTime]) = 0");
                    table.ForeignKey(
                        name: "FK_WorkCalendarOverrideIntervals_WorkCalendarDateOverrides_WorkCalendarDateOverrideId",
                        column: x => x.WorkCalendarDateOverrideId,
                        principalTable: "WorkCalendarDateOverrides",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_CalculationSnapshot",
                table: "EmployeeLeave",
                sql: "([CalculationSnapshotVersion] IS NULL AND [CalculationSnapshotJson] IS NULL) OR ([CalculationSnapshotVersion] IS NOT NULL AND [CalculationSnapshotVersion] > 0 AND [CalculationSnapshotJson] IS NOT NULL AND ISJSON([CalculationSnapshotJson]) = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_ChargeMinutes",
                table: "EmployeeLeave",
                sql: "[ChargeableMinutes] IS NULL OR [ChargeableMinutes] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeLeave_RequestTimes",
                table: "EmployeeLeave",
                sql: "([RequestedStartTime] IS NULL AND [RequestedEndTime] IS NULL) OR ([RequestedStartTime] IS NOT NULL AND [RequestedEndTime] IS NOT NULL AND DATEPART(SECOND,[RequestedStartTime]) = 0 AND DATEPART(SECOND,[RequestedEndTime]) = 0 AND ([EndDate] > [StartDate] OR ([EndDate] = [StartDate] AND [RequestedEndTime] > [RequestedStartTime])))");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEntitlementAdjustments_EmployeeLeaveEntitlementId",
                table: "EmployeeLeaveEntitlementAdjustments",
                column: "EmployeeLeaveEntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEntitlements_EmployeeId_LeaveTypeId_LeaveYear",
                table: "EmployeeLeaveEntitlements",
                columns: new[] { "EmployeeId", "LeaveTypeId", "LeaveYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEntitlements_LeaveTypeId",
                table: "EmployeeLeaveEntitlements",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkCalendarAssignments_EmployeeId_EffectiveFrom",
                table: "EmployeeWorkCalendarAssignments",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkCalendarAssignments_WorkCalendarId",
                table: "EmployeeWorkCalendarAssignments",
                column: "WorkCalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePolicies_DocumentTypeId",
                table: "LeavePolicies",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeavePolicies_LeaveTypeId_Status_EffectiveFrom",
                table: "LeavePolicies",
                columns: new[] { "LeaveTypeId", "Status", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_LeavePolicies_LeaveTypeId_Version",
                table: "LeavePolicies",
                columns: new[] { "LeaveTypeId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendarDateOverrides_WorkCalendarId_Date",
                table: "WorkCalendarDateOverrides",
                columns: new[] { "WorkCalendarId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendarOverrideIntervals_WorkCalendarDateOverrideId_StartTime",
                table: "WorkCalendarOverrideIntervals",
                columns: new[] { "WorkCalendarDateOverrideId", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendars_Code",
                table: "WorkCalendars",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendars_IsDefault",
                table: "WorkCalendars",
                column: "IsDefault",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCalendarWeeklyIntervals_WorkCalendarId_DayOfWeek_StartTime",
                table: "WorkCalendarWeeklyIntervals",
                columns: new[] { "WorkCalendarId", "DayOfWeek", "StartTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeLeaveEntitlementAdjustments");

            migrationBuilder.DropTable(
                name: "EmployeeWorkCalendarAssignments");

            migrationBuilder.DropTable(
                name: "LeavePolicies");

            migrationBuilder.DropTable(
                name: "WorkCalendarOverrideIntervals");

            migrationBuilder.DropTable(
                name: "WorkCalendarWeeklyIntervals");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveEntitlements");

            migrationBuilder.DropTable(
                name: "WorkCalendarDateOverrides");

            migrationBuilder.DropTable(
                name: "WorkCalendars");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_CalculationSnapshot",
                table: "EmployeeLeave");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_ChargeMinutes",
                table: "EmployeeLeave");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeLeave_RequestTimes",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "CalculationSnapshotJson",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "CalculationSnapshotVersion",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "ChargeableMinutes",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "RequestedEndTime",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "RequestedStartTime",
                table: "EmployeeLeave");
        }
    }
}
