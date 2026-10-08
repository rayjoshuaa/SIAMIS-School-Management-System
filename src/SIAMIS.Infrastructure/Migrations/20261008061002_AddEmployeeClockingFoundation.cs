using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeClockingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceEvent_Source",
                table: "AttendanceEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AttendanceEvents_EmployeeId_AttendanceEventId",
                table: "AttendanceEvents",
                columns: new[] { "EmployeeId", "AttendanceEventId" });

            migrationBuilder.CreateTable(
                name: "EmployeeClockSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkArrangement = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2"),
                    InEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeClockSessions", x => x.Id);
                    table.CheckConstraint("CK_EmployeeClockSession_Arrangement", "[WorkArrangement] IN ('OnCampus','OnlineClass','RemoteWork')");
                    table.CheckConstraint("CK_EmployeeClockSession_Events", "[OutEventId] IS NULL OR [OutEventId] <> [InEventId]");
                    table.ForeignKey(
                        name: "FK_EmployeeClockSessions_AttendanceEvents_EmployeeId_InEventId",
                        columns: x => new { x.EmployeeId, x.InEventId },
                        principalTable: "AttendanceEvents",
                        principalColumns: new[] { "EmployeeId", "AttendanceEventId" });
                    table.ForeignKey(
                        name: "FK_EmployeeClockSessions_AttendanceEvents_EmployeeId_OutEventId",
                        columns: x => new { x.EmployeeId, x.OutEventId },
                        principalTable: "AttendanceEvents",
                        principalColumns: new[] { "EmployeeId", "AttendanceEventId" });
                    table.ForeignKey(
                        name: "FK_EmployeeClockSessions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceEvent_Source",
                table: "AttendanceEvents",
                sql: "[Source] IN ('ManualAuthorized','Device','Imported','EmployeeClock')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents",
                sql: "([Source] = 'ManualAuthorized' AND [ManualRequestKey] IS NOT NULL AND [ManualRequestKey] <> '00000000-0000-0000-0000-000000000000' AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [SourceKey] IS NULL AND [ExternalEventId] IS NULL) OR ([Source] IN ('Device','Imported') AND [SourceKey] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceKey]))) > 0 AND [ExternalEventId] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalEventId]))) > 0 AND [ManualRequestKey] IS NULL) OR ([Source] = 'EmployeeClock' AND [Direction] IN ('In','Out') AND [ActorId] IS NOT NULL AND [SourceKey] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceKey]))) > 0 AND [ExternalEventId] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalEventId]))) > 0 AND [ManualRequestKey] IS NULL AND [OriginalSourceTimestamp] IS NULL AND [Reason] IS NULL AND [EmployeeWasInactive] = 0 AND [EmploymentReadiness] = 'Ready' AND [OccurredAtUtc] = [ReceivedAtUtc])");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeClockSessions_EmployeeId",
                table: "EmployeeClockSessions",
                column: "EmployeeId",
                unique: true,
                filter: "[OutEventId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeClockSessions_EmployeeId_InEventId",
                table: "EmployeeClockSessions",
                columns: new[] { "EmployeeId", "InEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeClockSessions_EmployeeId_OutEventId",
                table: "EmployeeClockSessions",
                columns: new[] { "EmployeeId", "OutEventId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeClockSessions_InEventId",
                table: "EmployeeClockSessions",
                column: "InEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeClockSessions_OutEventId",
                table: "EmployeeClockSessions",
                column: "OutEventId",
                unique: true,
                filter: "[OutEventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeClockSessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AttendanceEvents_EmployeeId_AttendanceEventId",
                table: "AttendanceEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceEvent_Source",
                table: "AttendanceEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceEvent_Source",
                table: "AttendanceEvents",
                sql: "[Source] IN ('ManualAuthorized','Device','Imported')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents",
                sql: "([Source] = 'ManualAuthorized' AND [ManualRequestKey] IS NOT NULL AND [ManualRequestKey] <> '00000000-0000-0000-0000-000000000000' AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [SourceKey] IS NULL AND [ExternalEventId] IS NULL) OR ([Source] IN ('Device','Imported') AND [SourceKey] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceKey]))) > 0 AND [ExternalEventId] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalEventId]))) > 0 AND [ManualRequestKey] IS NULL)");
        }
    }
}
