using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceEvidenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceEvents",
                columns: table => new
                {
                    AttendanceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BusinessTimeZone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SourceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ExternalEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true, collation: "Latin1_General_100_BIN2"),
                    ManualRequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalSourceTimestamp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeWasInactive = table.Column<bool>(type: "bit", nullable: false),
                    EmploymentReadiness = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceEvents", x => x.AttendanceEventId);
                    table.CheckConstraint("CK_AttendanceEvent_Direction", "[Direction] IN ('In','Out','Unknown')");
                    table.CheckConstraint("CK_AttendanceEvent_EmploymentReadiness", "[EmploymentReadiness] IN ('Ready','NotEmployed','ConfigurationConflict')");
                    table.CheckConstraint("CK_AttendanceEvent_Source", "[Source] IN ('ManualAuthorized','Device','Imported')");
                    table.CheckConstraint("CK_AttendanceEvent_SourceFields", "([Source] = 'ManualAuthorized' AND [ManualRequestKey] IS NOT NULL AND [ManualRequestKey] <> '00000000-0000-0000-0000-000000000000' AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [SourceKey] IS NULL AND [ExternalEventId] IS NULL AND [ActorId] IS NULL) OR ([Source] IN ('Device','Imported') AND [SourceKey] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceKey]))) > 0 AND [ExternalEventId] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalEventId]))) > 0 AND [ManualRequestKey] IS NULL)");
                    table.CheckConstraint("CK_AttendanceEvent_TimeZone", "[BusinessTimeZone] = 'Asia/Bangkok'");
                    table.ForeignKey(
                        name: "FK_AttendanceEvents_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvents_EmployeeId_BusinessDate_OccurredAtUtc_AttendanceEventId",
                table: "AttendanceEvents",
                columns: new[] { "EmployeeId", "BusinessDate", "OccurredAtUtc", "AttendanceEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvents_ManualRequestKey",
                table: "AttendanceEvents",
                column: "ManualRequestKey",
                unique: true,
                filter: "[ManualRequestKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceEvents_SourceKey_ExternalEventId",
                table: "AttendanceEvents",
                columns: new[] { "SourceKey", "ExternalEventId" },
                unique: true,
                filter: "[ExternalEventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceEvents");
        }
    }
}
