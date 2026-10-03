using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPitPayrollIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePitPaymentSchedules",
                columns: table => new
                {
                    EmployeePitPaymentScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ReplacesScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePitPaymentSchedules", x => x.EmployeePitPaymentScheduleId);
                    table.UniqueConstraint("AK_EmployeePitPaymentSchedules_EmployeeId_TaxYear_EmployeePitPaymentScheduleId", x => new { x.EmployeeId, x.TaxYear, x.EmployeePitPaymentScheduleId });
                    table.CheckConstraint("CK_PitSchedule_Evidence", "LEN(LTRIM(RTRIM([Evidence])))>0");
                    table.CheckConstraint("CK_PitSchedule_Status", "([Status]='Draft' AND [VerifiedAt] IS NULL) OR ([Status]='Verified' AND [VerifiedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_PitSchedule_YearRevision", "[TaxYear] BETWEEN 1 AND 9999 AND [RevisionNumber]>0");
                    table.ForeignKey(
                        name: "FK_EmployeePitPaymentSchedules_EmployeePitPaymentSchedules_EmployeeId_TaxYear_ReplacesScheduleId",
                        columns: x => new { x.EmployeeId, x.TaxYear, x.ReplacesScheduleId },
                        principalTable: "EmployeePitPaymentSchedules",
                        principalColumns: new[] { "EmployeeId", "TaxYear", "EmployeePitPaymentScheduleId" });
                    table.ForeignKey(
                        name: "FK_EmployeePitPaymentSchedules_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayrollPitResults",
                columns: table => new
                {
                    EmployeePayrollPitResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeePayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoverningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StatutorySchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutoryPolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeStatutoryEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeTaxDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeePitPaymentScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ApplicablePaymentCount = table.Column<int>(type: "int", nullable: false),
                    PaymentOrdinal = table.Column<int>(type: "int", nullable: false),
                    CalculationMethodVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentRegularIncome = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    PriorRecognizedIncome = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    ProjectedRegularIncome = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    EmploymentExpenseDeduction = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    PersonalAllowance = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    SpouseAllowance = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    ChildAllowance = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    ParentAllowance = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    RecognizedEmployeeSso = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    NetTaxableIncome = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    RawAnnualTax = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    AllocatableAnnualWithholding = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    SubSatangRemainder = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    PriorRecognizedWithholding = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    CurrentWithholding = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    FinalAllocationResidual = table.Column<decimal>(type: "decimal(38,18)", nullable: true),
                    IsFinalScheduledPayment = table.Column<bool>(type: "bit", nullable: false),
                    OverWithheldAmount = table.Column<decimal>(type: "decimal(38,18)", nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollPitResults", x => x.EmployeePayrollPitResultId);
                    table.CheckConstraint("CK_PitResult_Amount", "[CurrentWithholding] >= 0 AND [CurrentWithholding] = ROUND([CurrentWithholding], 2) AND [NetTaxableIncome] >= 0 AND [RecognizedEmployeeSso] >= 0");
                    table.CheckConstraint("CK_PitResult_Final", "([IsFinalScheduledPayment] = 1 AND [PaymentOrdinal] = [ApplicablePaymentCount] AND [FinalAllocationResidual] IS NOT NULL) OR ([IsFinalScheduledPayment] = 0 AND [PaymentOrdinal] < [ApplicablePaymentCount] AND [FinalAllocationResidual] IS NULL)");
                    table.CheckConstraint("CK_PitResult_Snapshot", "ISJSON([CalculationSnapshotJson]) = 1");
                    table.CheckConstraint("CK_PitResult_YearSchedule", "[TaxYear] = DATEPART(year, [GoverningDate]) AND [ApplicablePaymentCount] BETWEEN 1 AND 12 AND [PaymentOrdinal] BETWEEN 1 AND [ApplicablePaymentCount] AND [ScheduleRevisionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_EmployeePayrolls_EmployeePayrollId",
                        column: x => x.EmployeePayrollId,
                        principalTable: "EmployeePayrolls",
                        principalColumn: "EmployeePayrollId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_EmployeePitPaymentSchedules_EmployeePitPaymentScheduleId",
                        column: x => x.EmployeePitPaymentScheduleId,
                        principalTable: "EmployeePitPaymentSchedules",
                        principalColumn: "EmployeePitPaymentScheduleId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_EmployeeStatutoryEnrollments_EmployeeStatutoryEnrollmentId",
                        column: x => x.EmployeeStatutoryEnrollmentId,
                        principalTable: "EmployeeStatutoryEnrollments",
                        principalColumn: "EmployeeStatutoryEnrollmentId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_EmployeeTaxDeclarations_EmployeeTaxDeclarationId",
                        column: x => x.EmployeeTaxDeclarationId,
                        principalTable: "EmployeeTaxDeclarations",
                        principalColumn: "EmployeeTaxDeclarationId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "PayrollPeriodId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_StatutoryPolicyVersions_StatutoryPolicyVersionId",
                        column: x => x.StatutoryPolicyVersionId,
                        principalTable: "StatutoryPolicyVersions",
                        principalColumn: "StatutoryPolicyVersionId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollPitResults_StatutorySchemes_StatutorySchemeId",
                        column: x => x.StatutorySchemeId,
                        principalTable: "StatutorySchemes",
                        principalColumn: "StatutorySchemeId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeePitPaymentScheduleEntries",
                columns: table => new
                {
                    EmployeePitPaymentScheduleEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeePitPaymentScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentOrdinal = table.Column<int>(type: "int", nullable: false),
                    PlannedPayDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePitPaymentScheduleEntries", x => x.EmployeePitPaymentScheduleEntryId);
                    table.CheckConstraint("CK_PitScheduleEntry_Ordinal", "[PaymentOrdinal] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_EmployeePitPaymentScheduleEntries_EmployeePitPaymentSchedules_EmployeePitPaymentScheduleId",
                        column: x => x.EmployeePitPaymentScheduleId,
                        principalTable: "EmployeePitPaymentSchedules",
                        principalColumn: "EmployeePitPaymentScheduleId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeePitPaymentScheduleSelections",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    CurrentScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePitPaymentScheduleSelections", x => new { x.EmployeeId, x.TaxYear });
                    table.ForeignKey(
                        name: "FK_EmployeePitPaymentScheduleSelections_EmployeePitPaymentSchedules_EmployeeId_TaxYear_CurrentScheduleId",
                        columns: x => new { x.EmployeeId, x.TaxYear, x.CurrentScheduleId },
                        principalTable: "EmployeePitPaymentSchedules",
                        principalColumns: new[] { "EmployeeId", "TaxYear", "EmployeePitPaymentScheduleId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_EmployeeId_TaxYear_GoverningDate",
                table: "EmployeePayrollPitResults",
                columns: new[] { "EmployeeId", "TaxYear", "GoverningDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_EmployeePayrollId",
                table: "EmployeePayrollPitResults",
                column: "EmployeePayrollId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_EmployeePitPaymentScheduleId",
                table: "EmployeePayrollPitResults",
                column: "EmployeePitPaymentScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_EmployeeStatutoryEnrollmentId",
                table: "EmployeePayrollPitResults",
                column: "EmployeeStatutoryEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_EmployeeTaxDeclarationId",
                table: "EmployeePayrollPitResults",
                column: "EmployeeTaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_PayrollPeriodId",
                table: "EmployeePayrollPitResults",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_StatutoryPolicyVersionId",
                table: "EmployeePayrollPitResults",
                column: "StatutoryPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollPitResults_StatutorySchemeId",
                table: "EmployeePayrollPitResults",
                column: "StatutorySchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePitPaymentScheduleEntries_EmployeePitPaymentScheduleId_PaymentOrdinal",
                table: "EmployeePitPaymentScheduleEntries",
                columns: new[] { "EmployeePitPaymentScheduleId", "PaymentOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePitPaymentScheduleEntries_EmployeePitPaymentScheduleId_PlannedPayDate",
                table: "EmployeePitPaymentScheduleEntries",
                columns: new[] { "EmployeePitPaymentScheduleId", "PlannedPayDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePitPaymentSchedules_EmployeeId_TaxYear",
                table: "EmployeePitPaymentSchedules",
                columns: new[] { "EmployeeId", "TaxYear" },
                unique: true,
                filter: "[Status]='Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePitPaymentSchedules_EmployeeId_TaxYear_ReplacesScheduleId",
                table: "EmployeePitPaymentSchedules",
                columns: new[] { "EmployeeId", "TaxYear", "ReplacesScheduleId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePitPaymentSchedules_EmployeeId_TaxYear_RevisionNumber",
                table: "EmployeePitPaymentSchedules",
                columns: new[] { "EmployeeId", "TaxYear", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePitPaymentScheduleSelections_EmployeeId_TaxYear_CurrentScheduleId",
                table: "EmployeePitPaymentScheduleSelections",
                columns: new[] { "EmployeeId", "TaxYear", "CurrentScheduleId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayrollPitResults");

            migrationBuilder.DropTable(
                name: "EmployeePitPaymentScheduleEntries");

            migrationBuilder.DropTable(
                name: "EmployeePitPaymentScheduleSelections");

            migrationBuilder.DropTable(
                name: "EmployeePitPaymentSchedules");
        }
    }
}
