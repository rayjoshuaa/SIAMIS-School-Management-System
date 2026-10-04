using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceReviewFinalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceReviewCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2"),
                    OriginalCalculationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalSourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceReviewCases", x => x.Id);
                    table.UniqueConstraint("AK_AttendanceReviewCases_EmployeeId_BusinessDate_Id", x => new { x.EmployeeId, x.BusinessDate, x.Id });
                    table.CheckConstraint("CK_AttendanceReviewCase_Snapshot", "ISJSON([OriginalCalculationJson]) = 1 AND LEN([OriginalSourceFingerprint]) = 64");
                    table.CheckConstraint("CK_AttendanceReviewCase_State", "[State] IN ('Open','Resolved')");
                    table.ForeignKey(
                        name: "FK_AttendanceReviewCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "FinalizedAttendanceRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ReviewCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsConfirmedAbsent = table.Column<bool>(type: "bit", nullable: false),
                    IsLate = table.Column<bool>(type: "bit", nullable: true),
                    ScheduledMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    PresenceCoveredScheduledMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    ApprovedLeaveCoveredScheduledMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    UnexplainedScheduledMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    CoverageTruncationResidualMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinalizedAttendanceRevisions", x => x.Id);
                    table.UniqueConstraint("AK_FinalizedAttendanceRevisions_EmployeeId_BusinessDate_Id", x => new { x.EmployeeId, x.BusinessDate, x.Id });
                    table.CheckConstraint("CK_FinalizedAttendanceRevision_Coverage", "[ScheduledMilliseconds] >= 0 AND [PresenceCoveredScheduledMilliseconds] >= 0 AND [ApprovedLeaveCoveredScheduledMilliseconds] >= 0 AND [UnexplainedScheduledMilliseconds] >= 0 AND [CoverageTruncationResidualMilliseconds] >= 0 AND [ScheduledMilliseconds] = [PresenceCoveredScheduledMilliseconds] + [ApprovedLeaveCoveredScheduledMilliseconds] + [UnexplainedScheduledMilliseconds] + [CoverageTruncationResidualMilliseconds] AND ([IsConfirmedAbsent] = 0 OR ([ScheduledMilliseconds] > 0 AND [PresenceCoveredScheduledMilliseconds] = 0 AND [ApprovedLeaveCoveredScheduledMilliseconds] = 0))");
                    table.CheckConstraint("CK_FinalizedAttendanceRevision_Snapshot", "[Revision] > 0 AND LEN([SourceFingerprint]) = 64 AND ISJSON([SourcesJson]) = 1 AND ISJSON([SnapshotJson]) = 1");
                    table.ForeignKey(
                        name: "FK_FinalizedAttendanceRevisions_AttendanceReviewCases_EmployeeId_BusinessDate_ReviewCaseId",
                        columns: x => new { x.EmployeeId, x.BusinessDate, x.ReviewCaseId },
                        principalTable: "AttendanceReviewCases",
                        principalColumns: new[] { "EmployeeId", "BusinessDate", "Id" });
                    table.ForeignKey(
                        name: "FK_FinalizedAttendanceRevisions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "AttendanceReviewActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    ReviewCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AttendanceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalizedRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CalculationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origin = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceReviewActions", x => x.Id);
                    table.CheckConstraint("CK_AttendanceReviewAction_Shape", "[Sequence] > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND LEN([SourceFingerprint]) = 64 AND ISJSON([CalculationJson]) = 1 AND [Origin] = 'DevelopmentUnattributed' AND (([Action] IN ('CorrectionAdded','Excluded','Included') AND [AttendanceEventId] IS NOT NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'AbsenceConfirmed' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'Finalized' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL) OR ([Action] = 'Reopened' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL AND [ReviewCaseId] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_AttendanceReviewActions_AttendanceEvents_AttendanceEventId",
                        column: x => x.AttendanceEventId,
                        principalTable: "AttendanceEvents",
                        principalColumn: "AttendanceEventId");
                    table.ForeignKey(
                        name: "FK_AttendanceReviewActions_AttendanceReviewCases_EmployeeId_BusinessDate_ReviewCaseId",
                        columns: x => new { x.EmployeeId, x.BusinessDate, x.ReviewCaseId },
                        principalTable: "AttendanceReviewCases",
                        principalColumns: new[] { "EmployeeId", "BusinessDate", "Id" });
                    table.ForeignKey(
                        name: "FK_AttendanceReviewActions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_AttendanceReviewActions_FinalizedAttendanceRevisions_EmployeeId_BusinessDate_FinalizedRevisionId",
                        columns: x => new { x.EmployeeId, x.BusinessDate, x.FinalizedRevisionId },
                        principalTable: "FinalizedAttendanceRevisions",
                        principalColumns: new[] { "EmployeeId", "BusinessDate", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReviewActions_AttendanceEventId",
                table: "AttendanceReviewActions",
                column: "AttendanceEventId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReviewActions_EmployeeId_BusinessDate_FinalizedRevisionId",
                table: "AttendanceReviewActions",
                columns: new[] { "EmployeeId", "BusinessDate", "FinalizedRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReviewActions_EmployeeId_BusinessDate_ReviewCaseId",
                table: "AttendanceReviewActions",
                columns: new[] { "EmployeeId", "BusinessDate", "ReviewCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReviewActions_EmployeeId_BusinessDate_Sequence",
                table: "AttendanceReviewActions",
                columns: new[] { "EmployeeId", "BusinessDate", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReviewActions_FinalizedRevisionId",
                table: "AttendanceReviewActions",
                column: "FinalizedRevisionId",
                unique: true,
                filter: "[Action] = 'Reopened'");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceReviewCases_EmployeeId_BusinessDate",
                table: "AttendanceReviewCases",
                columns: new[] { "EmployeeId", "BusinessDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinalizedAttendanceRevisions_EmployeeId_BusinessDate_ReviewCaseId",
                table: "FinalizedAttendanceRevisions",
                columns: new[] { "EmployeeId", "BusinessDate", "ReviewCaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinalizedAttendanceRevisions_EmployeeId_BusinessDate_Revision",
                table: "FinalizedAttendanceRevisions",
                columns: new[] { "EmployeeId", "BusinessDate", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceReviewActions");

            migrationBuilder.DropTable(
                name: "FinalizedAttendanceRevisions");

            migrationBuilder.DropTable(
                name: "AttendanceReviewCases");
        }
    }
}
