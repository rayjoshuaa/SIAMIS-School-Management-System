using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSection33PayrollResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_SourceTypeAndId",
                table: "EmployeePayrollLines");

            migrationBuilder.CreateTable(
                name: "EmployeePayrollStatutoryResults",
                columns: table => new
                {
                    EmployeePayrollStatutoryResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeePayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutorySchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutoryPolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeStatutoryEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalculationMethodVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContributionMonth = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    GoverningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollStatutoryResults", x => x.EmployeePayrollStatutoryResultId);
                    table.CheckConstraint("CK_PayrollStatutoryResult_Json", "ISJSON([CalculationSnapshotJson]) = 1");
                    table.CheckConstraint("CK_PayrollStatutoryResult_Month", "[ContributionMonth] = CONVERT(char(7), [GoverningDate], 126)");
                    table.CheckConstraint("CK_PayrollStatutoryResult_V1", "[CalculationMethodVersion] = 'SSO-TH-V1' AND [Currency] = 'THB'");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollStatutoryResults_EmployeePayrolls_EmployeePayrollId",
                        column: x => x.EmployeePayrollId,
                        principalTable: "EmployeePayrolls",
                        principalColumn: "EmployeePayrollId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollStatutoryResults_EmployeeStatutoryEnrollments_EmployeeStatutoryEnrollmentId",
                        column: x => x.EmployeeStatutoryEnrollmentId,
                        principalTable: "EmployeeStatutoryEnrollments",
                        principalColumn: "EmployeeStatutoryEnrollmentId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollStatutoryResults_StatutoryPolicyVersions_StatutoryPolicyVersionId",
                        column: x => x.StatutoryPolicyVersionId,
                        principalTable: "StatutoryPolicyVersions",
                        principalColumn: "StatutoryPolicyVersionId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollStatutoryResults_StatutorySchemes_StatutorySchemeId",
                        column: x => x.StatutorySchemeId,
                        principalTable: "StatutorySchemes",
                        principalColumn: "StatutorySchemeId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayrollSocialSecurityResults",
                columns: table => new
                {
                    EmployeePayrollStatutoryResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContributionWage = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    ContributionBase = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    MinimumBase = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    MaximumBase = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    EmployeeRate = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    EmployerRate = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    RawEmployeeAmount = table.Column<decimal>(type: "decimal(38,10)", nullable: false),
                    EmployeeAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    EmployerAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollSocialSecurityResults", x => x.EmployeePayrollStatutoryResultId);
                    table.CheckConstraint("CK_PayrollSsoResult_Base", "([ContributionWage] = 0 AND [ContributionBase] = 0 AND [RawEmployeeAmount] = 0 AND [EmployeeAmount] = 0) OR ([ContributionWage] > 0 AND [ContributionBase] >= [MinimumBase] AND [ContributionBase] <= [MaximumBase])");
                    table.CheckConstraint("CK_PayrollSsoResult_NonNegative", "[ContributionWage] >= 0 AND [ContributionBase] >= 0 AND [MinimumBase] >= 0 AND [MaximumBase] >= [MinimumBase] AND [EmployeeRate] >= 0 AND [EmployerRate] >= 0 AND [RawEmployeeAmount] >= 0 AND [EmployeeAmount] >= 0 AND [EmployerAmount] >= 0");
                    table.CheckConstraint("CK_PayrollSsoResult_V1Amounts", "[EmployeeRate] = [EmployerRate] AND [EmployerAmount] = [EmployeeAmount] AND [EmployeeAmount] = FLOOR([EmployeeAmount])");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollSocialSecurityResults_EmployeePayrollStatutoryResults_EmployeePayrollStatutoryResultId",
                        column: x => x.EmployeePayrollStatutoryResultId,
                        principalTable: "EmployeePayrollStatutoryResults",
                        principalColumn: "EmployeePayrollStatutoryResultId");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_SourceTypeAndId",
                table: "EmployeePayrollLines",
                sql: "([SourceType] = 'BasicSalary' AND [SourceId] IS NULL) OR ([SourceType] = 'Manual' AND [SourceId] IS NULL) OR ([SourceType] IN ('Assignment', 'PayrollRule', 'Statutory') AND [SourceId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollStatutoryResults_EmployeePayrollId_StatutorySchemeId",
                table: "EmployeePayrollStatutoryResults",
                columns: new[] { "EmployeePayrollId", "StatutorySchemeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollStatutoryResults_EmployeeStatutoryEnrollmentId",
                table: "EmployeePayrollStatutoryResults",
                column: "EmployeeStatutoryEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollStatutoryResults_StatutoryPolicyVersionId",
                table: "EmployeePayrollStatutoryResults",
                column: "StatutoryPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollStatutoryResults_StatutorySchemeId",
                table: "EmployeePayrollStatutoryResults",
                column: "StatutorySchemeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayrollSocialSecurityResults");

            migrationBuilder.DropTable(
                name: "EmployeePayrollStatutoryResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeePayrollLines_SourceTypeAndId",
                table: "EmployeePayrollLines");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeePayrollLines_SourceTypeAndId",
                table: "EmployeePayrollLines",
                sql: "([SourceType] = 'BasicSalary' AND [SourceId] IS NULL) OR ([SourceType] = 'Manual' AND [SourceId] IS NULL) OR ([SourceType] IN ('Assignment', 'PayrollRule') AND [SourceId] IS NOT NULL)");
        }
    }
}
