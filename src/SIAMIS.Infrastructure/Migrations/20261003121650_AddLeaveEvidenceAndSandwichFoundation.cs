using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveEvidenceAndSandwichFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SandwichEquivalentDayMinutes",
                table: "LeavePolicies",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmployeeLeave_EmployeeId_LeaveId",
                table: "EmployeeLeave",
                columns: new[] { "EmployeeId", "LeaveId" });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupersedesEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveEvidence", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeLeaveEvidence_EmployeeId_LeaveId_Id", x => new { x.EmployeeId, x.LeaveId, x.Id });
                    table.CheckConstraint("CK_LeaveEvidence_Kind", "[EvidenceKind] = 'ExternalReceipt'");
                    table.CheckConstraint("CK_LeaveEvidence_Reference", "LEN(LTRIM(RTRIM([ExternalReference]))) > 0");
                    table.CheckConstraint("CK_LeaveEvidence_Successor", "[SupersedesEvidenceId] IS NULL OR [SupersedesEvidenceId] <> [Id]");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEvidence_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEvidence_EmployeeLeaveEvidence_EmployeeId_LeaveId_SupersedesEvidenceId",
                        columns: x => new { x.EmployeeId, x.LeaveId, x.SupersedesEvidenceId },
                        principalTable: "EmployeeLeaveEvidence",
                        principalColumns: new[] { "EmployeeId", "LeaveId", "Id" });
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEvidence_EmployeeLeave_EmployeeId_LeaveId",
                        columns: x => new { x.EmployeeId, x.LeaveId },
                        principalTable: "EmployeeLeave",
                        principalColumns: new[] { "EmployeeId", "LeaveId" });
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveSandwichCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BeforeLeaveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AfterLeaveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GapStart = table.Column<DateOnly>(type: "date", nullable: false),
                    GapEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    BalanceTracked = table.Column<bool>(type: "bit", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveSandwichCases", x => x.Id);
                    table.UniqueConstraint("AK_EmployeeLeaveSandwichCases_EmployeeId_Id", x => new { x.EmployeeId, x.Id });
                    table.CheckConstraint("CK_LeaveSandwich_Active", "([State] = 'Released' AND [IsActive] = 0) OR ([State] <> 'Released' AND [IsActive] = 1)");
                    table.CheckConstraint("CK_LeaveSandwich_Span", "[GapStart] <= [GapEnd] AND [Revision] > 0");
                    table.CheckConstraint("CK_LeaveSandwich_State", "[State] IN ('Reserved','Charged','Exempted','Released')");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveSandwichCases_EmployeeLeave_EmployeeId_AfterLeaveId",
                        columns: x => new { x.EmployeeId, x.AfterLeaveId },
                        principalTable: "EmployeeLeave",
                        principalColumns: new[] { "EmployeeId", "LeaveId" });
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveSandwichCases_EmployeeLeave_EmployeeId_BeforeLeaveId",
                        columns: x => new { x.EmployeeId, x.BeforeLeaveId },
                        principalTable: "EmployeeLeave",
                        principalColumns: new[] { "EmployeeId", "LeaveId" });
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveSandwichCases_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveApprovalEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveApprovalEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveApprovalEvidence_EmployeeLeaveEvidence_EmployeeId_LeaveId_EvidenceId",
                        columns: x => new { x.EmployeeId, x.LeaveId, x.EvidenceId },
                        principalTable: "EmployeeLeaveEvidence",
                        principalColumns: new[] { "EmployeeId", "LeaveId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveEvidenceEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveEvidenceEvents", x => x.Id);
                    table.CheckConstraint("CK_LeaveEvidenceEvent_Action", "[Action] IN ('Recorded','Accepted','Rejected','Superseded')");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveEvidenceEvents_EmployeeLeaveEvidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "EmployeeLeaveEvidence",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveSandwichAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveYear = table.Column<int>(type: "int", nullable: false),
                    SandwichDebitMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveSandwichAllocations", x => x.Id);
                    table.CheckConstraint("CK_LeaveSandwichAllocation_Debit", "[LeaveYear] BETWEEN 1 AND 9999 AND [SandwichDebitMinutes] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveSandwichAllocations_EmployeeLeaveSandwichCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "EmployeeLeaveSandwichCases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveSandwichDates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledMinutes = table.Column<int>(type: "int", nullable: false),
                    SandwichDebitMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveSandwichDates", x => x.Id);
                    table.CheckConstraint("CK_LeaveSandwichDate_Debit", "[ScheduledMinutes] = 0 AND [SandwichDebitMinutes] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveSandwichDates_EmployeeLeaveSandwichCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "EmployeeLeaveSandwichCases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveSandwichEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CausingLeaveId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveSandwichEvents", x => x.Id);
                    table.CheckConstraint("CK_LeaveSandwichEvent_State", "[State] IN ('Reserved','Charged','Exempted','Released')");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveSandwichEvents_EmployeeLeaveSandwichCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "EmployeeLeaveSandwichCases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveSandwichEvents_EmployeeLeave_CausingLeaveId",
                        column: x => x.CausingLeaveId,
                        principalTable: "EmployeeLeave",
                        principalColumn: "LeaveId");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeavePolicy_SandwichMinutes",
                table: "LeavePolicies",
                sql: "([SandwichEquivalentDayMinutes] IS NULL OR [SandwichEquivalentDayMinutes] > 0) AND ([SandwichParticipation] = 1 OR [SandwichEquivalentDayMinutes] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveApprovalEvidence_EmployeeId_LeaveId_EvidenceId",
                table: "EmployeeLeaveApprovalEvidence",
                columns: new[] { "EmployeeId", "LeaveId", "EvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveApprovalEvidence_LeaveId_EvidenceId",
                table: "EmployeeLeaveApprovalEvidence",
                columns: new[] { "LeaveId", "EvidenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEvidence_DocumentTypeId",
                table: "EmployeeLeaveEvidence",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEvidence_EmployeeId_LeaveId_SupersedesEvidenceId",
                table: "EmployeeLeaveEvidence",
                columns: new[] { "EmployeeId", "LeaveId", "SupersedesEvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEvidence_SupersedesEvidenceId",
                table: "EmployeeLeaveEvidence",
                column: "SupersedesEvidenceId",
                unique: true,
                filter: "[SupersedesEvidenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveEvidenceEvents_EvidenceId",
                table: "EmployeeLeaveEvidenceEvents",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichAllocations_CaseId_LeaveYear",
                table: "EmployeeLeaveSandwichAllocations",
                columns: new[] { "CaseId", "LeaveYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichCases_EmployeeId_AfterLeaveId",
                table: "EmployeeLeaveSandwichCases",
                columns: new[] { "EmployeeId", "AfterLeaveId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichCases_EmployeeId_BeforeLeaveId",
                table: "EmployeeLeaveSandwichCases",
                columns: new[] { "EmployeeId", "BeforeLeaveId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichCases_EmployeeId_GapStart_GapEnd",
                table: "EmployeeLeaveSandwichCases",
                columns: new[] { "EmployeeId", "GapStart", "GapEnd" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichCases_EmployeeId_GapStart_GapEnd_Revision",
                table: "EmployeeLeaveSandwichCases",
                columns: new[] { "EmployeeId", "GapStart", "GapEnd", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichCases_LeaveTypeId",
                table: "EmployeeLeaveSandwichCases",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichDates_CaseId_Date",
                table: "EmployeeLeaveSandwichDates",
                columns: new[] { "CaseId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichEvents_CaseId",
                table: "EmployeeLeaveSandwichEvents",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveSandwichEvents_CausingLeaveId",
                table: "EmployeeLeaveSandwichEvents",
                column: "CausingLeaveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeLeaveApprovalEvidence");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveEvidenceEvents");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveSandwichAllocations");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveSandwichDates");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveSandwichEvents");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveEvidence");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveSandwichCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeavePolicy_SandwichMinutes",
                table: "LeavePolicies");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmployeeLeave_EmployeeId_LeaveId",
                table: "EmployeeLeave");

            migrationBuilder.DropColumn(
                name: "SandwichEquivalentDayMinutes",
                table: "LeavePolicies");
        }
    }
}
