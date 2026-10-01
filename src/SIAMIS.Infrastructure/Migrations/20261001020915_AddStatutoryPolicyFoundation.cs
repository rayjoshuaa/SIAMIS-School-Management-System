using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutoryPolicyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatutorySchemes",
                columns: table => new
                {
                    StatutorySchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Jurisdiction = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    SchemeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutorySchemes", x => x.StatutorySchemeId);
                    table.UniqueConstraint("AK_StatutorySchemes_StatutorySchemeId_SchemeType", x => new { x.StatutorySchemeId, x.SchemeType });
                    table.CheckConstraint("CK_StatutorySchemes_Jurisdiction", "[Jurisdiction] = 'TH'");
                    table.CheckConstraint("CK_StatutorySchemes_Type", "[SchemeType] IN ('SocialSecurity','PersonalIncomeTax')");
                });

            migrationBuilder.CreateTable(
                name: "StatutoryPolicyVersions",
                columns: table => new
                {
                    StatutoryPolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutorySchemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OfficialReference = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CalculationMethodVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryPolicyVersions", x => x.StatutoryPolicyVersionId);
                    table.UniqueConstraint("AK_StatutoryPolicyVersions_StatutoryPolicyVersionId_SchemeType", x => new { x.StatutoryPolicyVersionId, x.SchemeType });
                    table.CheckConstraint("CK_StatutoryPolicyVersions_Currency", "[Currency] = 'THB'");
                    table.CheckConstraint("CK_StatutoryPolicyVersions_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_StatutoryPolicyVersions_Publication", "([Status] = 'Draft' AND [PublishedAt] IS NULL) OR ([Status] = 'Published' AND [PublishedAt] IS NOT NULL AND LEN(LTRIM(RTRIM([OfficialReference]))) > 0 AND [OfficialReference] IS NOT NULL AND LEN(LTRIM(RTRIM([CalculationMethodVersion]))) > 0 AND [CalculationMethodVersion] IS NOT NULL)");
                    table.CheckConstraint("CK_StatutoryPolicyVersions_Status", "[Status] IN ('Draft','Published')");
                    table.ForeignKey(
                        name: "FK_StatutoryPolicyVersions_StatutorySchemes_StatutorySchemeId_SchemeType",
                        columns: x => new { x.StatutorySchemeId, x.SchemeType },
                        principalTable: "StatutorySchemes",
                        principalColumns: new[] { "StatutorySchemeId", "SchemeType" });
                });

            migrationBuilder.CreateTable(
                name: "PitPolicyConfigurations",
                columns: table => new
                {
                    StatutoryPolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: true),
                    EmploymentExpenseDeductionRate = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    EmploymentExpenseDeductionCap = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    PersonalAllowanceAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    WithholdingMethodIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PitPolicyConfigurations", x => x.StatutoryPolicyVersionId);
                    table.CheckConstraint("CK_PitPolicyConfigurations_TaxYear", "[TaxYear] IS NULL OR [TaxYear] BETWEEN 1 AND 9999");
                    table.CheckConstraint("CK_PitPolicyConfigurations_Type", "[SchemeType] = 'PersonalIncomeTax'");
                    table.CheckConstraint("CK_PitPolicyConfigurations_Values", "([EmploymentExpenseDeductionRate] IS NULL OR [EmploymentExpenseDeductionRate] >= 0) AND ([EmploymentExpenseDeductionCap] IS NULL OR [EmploymentExpenseDeductionCap] >= 0) AND ([PersonalAllowanceAmount] IS NULL OR [PersonalAllowanceAmount] >= 0)");
                    table.ForeignKey(
                        name: "FK_PitPolicyConfigurations_StatutoryPolicyVersions_StatutoryPolicyVersionId_SchemeType",
                        columns: x => new { x.StatutoryPolicyVersionId, x.SchemeType },
                        principalTable: "StatutoryPolicyVersions",
                        principalColumns: new[] { "StatutoryPolicyVersionId", "SchemeType" });
                });

            migrationBuilder.CreateTable(
                name: "SocialSecurityPolicyConfigurations",
                columns: table => new
                {
                    StatutoryPolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeContributionRate = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    EmployerContributionRate = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    MinimumContributionBase = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    MaximumContributionBase = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    InsuredPersonClassification = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialSecurityPolicyConfigurations", x => x.StatutoryPolicyVersionId);
                    table.CheckConstraint("CK_SocialSecurityPolicyConfigurations_BaseRange", "[MinimumContributionBase] IS NULL OR [MaximumContributionBase] IS NULL OR [MaximumContributionBase] >= [MinimumContributionBase]");
                    table.CheckConstraint("CK_SocialSecurityPolicyConfigurations_Type", "[SchemeType] = 'SocialSecurity'");
                    table.CheckConstraint("CK_SocialSecurityPolicyConfigurations_Values", "([EmployeeContributionRate] IS NULL OR [EmployeeContributionRate] >= 0) AND ([EmployerContributionRate] IS NULL OR [EmployerContributionRate] >= 0) AND ([MinimumContributionBase] IS NULL OR [MinimumContributionBase] >= 0) AND ([MaximumContributionBase] IS NULL OR [MaximumContributionBase] >= 0)");
                    table.ForeignKey(
                        name: "FK_SocialSecurityPolicyConfigurations_StatutoryPolicyVersions_StatutoryPolicyVersionId_SchemeType",
                        columns: x => new { x.StatutoryPolicyVersionId, x.SchemeType },
                        principalTable: "StatutoryPolicyVersions",
                        principalColumns: new[] { "StatutoryPolicyVersionId", "SchemeType" });
                });

            migrationBuilder.CreateTable(
                name: "PitTaxBrackets",
                columns: table => new
                {
                    PitTaxBracketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatutoryPolicyVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LowerBoundInclusive = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    UpperBoundExclusive = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PitTaxBrackets", x => x.PitTaxBracketId);
                    table.CheckConstraint("CK_PitTaxBrackets_Values", "[SortOrder] > 0 AND [LowerBoundInclusive] >= 0 AND [Rate] >= 0 AND ([UpperBoundExclusive] IS NULL OR [UpperBoundExclusive] > [LowerBoundInclusive])");
                    table.ForeignKey(
                        name: "FK_PitTaxBrackets_PitPolicyConfigurations_StatutoryPolicyVersionId",
                        column: x => x.StatutoryPolicyVersionId,
                        principalTable: "PitPolicyConfigurations",
                        principalColumn: "StatutoryPolicyVersionId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PitPolicyConfigurations_StatutoryPolicyVersionId_SchemeType",
                table: "PitPolicyConfigurations",
                columns: new[] { "StatutoryPolicyVersionId", "SchemeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PitTaxBrackets_LowerBound",
                table: "PitTaxBrackets",
                columns: new[] { "StatutoryPolicyVersionId", "LowerBoundInclusive" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PitTaxBrackets_Order",
                table: "PitTaxBrackets",
                columns: new[] { "StatutoryPolicyVersionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialSecurityPolicyConfigurations_StatutoryPolicyVersionId_SchemeType",
                table: "SocialSecurityPolicyConfigurations",
                columns: new[] { "StatutoryPolicyVersionId", "SchemeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryPolicyVersions_Resolution",
                table: "StatutoryPolicyVersions",
                columns: new[] { "StatutorySchemeId", "Status", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryPolicyVersions_StatutorySchemeId_SchemeType",
                table: "StatutoryPolicyVersions",
                columns: new[] { "StatutorySchemeId", "SchemeType" });

            migrationBuilder.CreateIndex(
                name: "UX_StatutoryPolicyVersions_SchemeVersion",
                table: "StatutoryPolicyVersions",
                columns: new[] { "StatutorySchemeId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_StatutorySchemes_Code",
                table: "StatutorySchemes",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PitTaxBrackets");

            migrationBuilder.DropTable(
                name: "SocialSecurityPolicyConfigurations");

            migrationBuilder.DropTable(
                name: "PitPolicyConfigurations");

            migrationBuilder.DropTable(
                name: "StatutoryPolicyVersions");

            migrationBuilder.DropTable(
                name: "StatutorySchemes");
        }
    }
}
