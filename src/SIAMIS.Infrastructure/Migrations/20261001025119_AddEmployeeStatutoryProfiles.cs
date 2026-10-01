using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeStatutoryProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeStatutoryEnrollments",
                columns: table => new
                {
                    EmployeeStatutoryEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutorySchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Applicability = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MembershipNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeStatutoryEnrollments", x => x.EmployeeStatutoryEnrollmentId);
                    table.CheckConstraint("CK_EmployeeStatutoryEnrollments_Applicability", "[Applicability] IN ('Applicable','NotApplicable')");
                    table.CheckConstraint("CK_EmployeeStatutoryEnrollments_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_EmployeeStatutoryEnrollments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeeStatutoryEnrollments_StatutorySchemes_StatutorySchemeId",
                        column: x => x.StatutorySchemeId,
                        principalTable: "StatutorySchemes",
                        principalColumn: "StatutorySchemeId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTaxDeclarations",
                columns: table => new
                {
                    EmployeeTaxDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ReplacesDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TaxpayerIdentificationNumberSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTaxDeclarations", x => x.EmployeeTaxDeclarationId);
                    table.UniqueConstraint("AK_EmployeeTaxDeclarations_EmployeeId_TaxYear_EmployeeTaxDeclarationId", x => new { x.EmployeeId, x.TaxYear, x.EmployeeTaxDeclarationId });
                    table.CheckConstraint("CK_EmployeeTaxDeclarations_Replacement", "[ReplacesDeclarationId] IS NULL OR [ReplacesDeclarationId] <> [EmployeeTaxDeclarationId]");
                    table.CheckConstraint("CK_EmployeeTaxDeclarations_Status", "[Status] IN ('Draft','Verified')");
                    table.CheckConstraint("CK_EmployeeTaxDeclarations_Verification", "([Status]='Draft' AND [VerifiedAt] IS NULL AND [TaxpayerIdentificationNumberSnapshot] IS NULL) OR ([Status]='Verified' AND [VerifiedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_EmployeeTaxDeclarations_YearRevision", "[TaxYear] BETWEEN 1 AND 9999 AND [RevisionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_EmployeeTaxDeclarations_EmployeeTaxDeclarations_EmployeeId_TaxYear_ReplacesDeclarationId",
                        columns: x => new { x.EmployeeId, x.TaxYear, x.ReplacesDeclarationId },
                        principalTable: "EmployeeTaxDeclarations",
                        principalColumns: new[] { "EmployeeId", "TaxYear", "EmployeeTaxDeclarationId" });
                    table.ForeignKey(
                        name: "FK_EmployeeTaxDeclarations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTaxProfiles",
                columns: table => new
                {
                    EmployeeTaxProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxpayerIdentificationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTaxProfiles", x => x.EmployeeTaxProfileId);
                    table.ForeignKey(
                        name: "FK_EmployeeTaxProfiles_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTaxClaims",
                columns: table => new
                {
                    EmployeeTaxClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeTaxDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTaxClaims", x => x.EmployeeTaxClaimId);
                    table.CheckConstraint("CK_EmployeeTaxClaims_Type", "[ClaimType] IN ('Spouse','Child','Parent')");
                    table.CheckConstraint("CK_EmployeeTaxClaims_Values", "([Amount] IS NULL OR [Amount]>=0) AND ([Quantity] IS NULL OR [Quantity]>0)");
                    table.ForeignKey(
                        name: "FK_EmployeeTaxClaims_EmployeeTaxDeclarations_EmployeeTaxDeclarationId",
                        column: x => x.EmployeeTaxDeclarationId,
                        principalTable: "EmployeeTaxDeclarations",
                        principalColumn: "EmployeeTaxDeclarationId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTaxDeclarationSelections",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    CurrentDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTaxDeclarationSelections", x => new { x.EmployeeId, x.TaxYear });
                    table.CheckConstraint("CK_EmployeeTaxDeclarationSelections_Year", "[TaxYear] BETWEEN 1 AND 9999");
                    table.ForeignKey(
                        name: "FK_EmployeeTaxDeclarationSelections_EmployeeTaxDeclarations_EmployeeId_TaxYear_CurrentDeclarationId",
                        columns: x => new { x.EmployeeId, x.TaxYear, x.CurrentDeclarationId },
                        principalTable: "EmployeeTaxDeclarations",
                        principalColumns: new[] { "EmployeeId", "TaxYear", "EmployeeTaxDeclarationId" });
                });

            migrationBuilder.CreateTable(
                name: "EmployeeTaxOpeningBalances",
                columns: table => new
                {
                    EmployeeTaxDeclarationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PriorTaxableEmploymentIncome = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    PriorTaxWithheld = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    PriorSocialSecurityContribution = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTaxOpeningBalances", x => x.EmployeeTaxDeclarationId);
                    table.CheckConstraint("CK_EmployeeTaxOpeningBalances_Consistency", "([State]='Unknown' AND [PriorTaxableEmploymentIncome] IS NULL AND [PriorTaxWithheld] IS NULL AND [PriorSocialSecurityContribution] IS NULL AND [VerifiedAt] IS NULL) OR ([State] IN ('ConfirmedZero','VerifiedAmount') AND [PriorTaxableEmploymentIncome] IS NOT NULL AND [PriorTaxWithheld] IS NOT NULL AND [PriorSocialSecurityContribution] IS NOT NULL AND [VerifiedAt] IS NOT NULL AND [Remarks] IS NOT NULL AND LEN(LTRIM(RTRIM([Remarks])))>0 AND ([State]='VerifiedAmount' OR ([PriorTaxableEmploymentIncome]=0 AND [PriorTaxWithheld]=0 AND [PriorSocialSecurityContribution]=0)))");
                    table.CheckConstraint("CK_EmployeeTaxOpeningBalances_Currency", "[Currency]='THB'");
                    table.CheckConstraint("CK_EmployeeTaxOpeningBalances_State", "[State] IN ('Unknown','ConfirmedZero','VerifiedAmount')");
                    table.CheckConstraint("CK_EmployeeTaxOpeningBalances_Values", "([PriorTaxableEmploymentIncome] IS NULL OR [PriorTaxableEmploymentIncome]>=0) AND ([PriorTaxWithheld] IS NULL OR [PriorTaxWithheld]>=0) AND ([PriorSocialSecurityContribution] IS NULL OR [PriorSocialSecurityContribution]>=0)");
                    table.ForeignKey(
                        name: "FK_EmployeeTaxOpeningBalances_EmployeeTaxDeclarations_EmployeeTaxDeclarationId",
                        column: x => x.EmployeeTaxDeclarationId,
                        principalTable: "EmployeeTaxDeclarations",
                        principalColumn: "EmployeeTaxDeclarationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatutoryEnrollments_EmployeeId_StatutorySchemeId_EffectiveFrom_EffectiveTo",
                table: "EmployeeStatutoryEnrollments",
                columns: new[] { "EmployeeId", "StatutorySchemeId", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeStatutoryEnrollments_StatutorySchemeId",
                table: "EmployeeStatutoryEnrollments",
                column: "StatutorySchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTaxClaims_EmployeeTaxDeclarationId",
                table: "EmployeeTaxClaims",
                column: "EmployeeTaxDeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTaxDeclarations_EmployeeId_TaxYear_ReplacesDeclarationId",
                table: "EmployeeTaxDeclarations",
                columns: new[] { "EmployeeId", "TaxYear", "ReplacesDeclarationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTaxDeclarations_EmployeeId_TaxYear_RevisionNumber",
                table: "EmployeeTaxDeclarations",
                columns: new[] { "EmployeeId", "TaxYear", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_EmployeeTaxDeclarations_OneDraft",
                table: "EmployeeTaxDeclarations",
                columns: new[] { "EmployeeId", "TaxYear" },
                unique: true,
                filter: "[Status] = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTaxDeclarationSelections_EmployeeId_TaxYear_CurrentDeclarationId",
                table: "EmployeeTaxDeclarationSelections",
                columns: new[] { "EmployeeId", "TaxYear", "CurrentDeclarationId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTaxProfiles_EmployeeId",
                table: "EmployeeTaxProfiles",
                column: "EmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeStatutoryEnrollments");

            migrationBuilder.DropTable(
                name: "EmployeeTaxClaims");

            migrationBuilder.DropTable(
                name: "EmployeeTaxDeclarationSelections");

            migrationBuilder.DropTable(
                name: "EmployeeTaxOpeningBalances");

            migrationBuilder.DropTable(
                name: "EmployeeTaxProfiles");

            migrationBuilder.DropTable(
                name: "EmployeeTaxDeclarations");
        }
    }
}
