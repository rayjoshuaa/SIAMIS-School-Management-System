using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPitClaimsAndOpeningContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalChildAllowanceAmount",
                table: "PitPolicyConfigurations",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdoptedChildCombinedCountLimit",
                table: "PitPolicyConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChildAllowanceAmount",
                table: "PitPolicyConfigurations",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumEligibleParentCount",
                table: "PitPolicyConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ParentAllowanceAmount",
                table: "PitPolicyConfigurations",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpouseAllowanceAmount",
                table: "PitPolicyConfigurations",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompletenessAttested",
                table: "EmployeeTaxOpeningBalances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InputContractVersion",
                table: "EmployeeTaxOpeningBalances",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpeningBalanceScope",
                table: "EmployeeTaxOpeningBalances",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalLivingLawfulChildren",
                table: "EmployeeTaxDeclarations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdditionalChildAllowanceEligible",
                table: "EmployeeTaxClaims",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChildRelationshipType",
                table: "EmployeeTaxClaims",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PitPolicyConfigurations_ClaimValues",
                table: "PitPolicyConfigurations",
                sql: "([SpouseAllowanceAmount] IS NULL OR [SpouseAllowanceAmount]>=0) AND ([ChildAllowanceAmount] IS NULL OR [ChildAllowanceAmount]>=0) AND ([AdditionalChildAllowanceAmount] IS NULL OR [AdditionalChildAllowanceAmount]>=0) AND ([ParentAllowanceAmount] IS NULL OR [ParentAllowanceAmount]>=0) AND ([AdoptedChildCombinedCountLimit] IS NULL OR [AdoptedChildCombinedCountLimit]>0) AND ([MaximumEligibleParentCount] IS NULL OR [MaximumEligibleParentCount]>0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeTaxOpeningBalances_Scope",
                table: "EmployeeTaxOpeningBalances",
                sql: "([OpeningBalanceScope] IS NULL OR [OpeningBalanceScope]='CurrentEmployer') AND ([InputContractVersion] IS NULL OR ([InputContractVersion]='PIT-TH-V1' AND [OpeningBalanceScope] IS NOT NULL AND [CompletenessAttested]=1 AND [State] IN ('ConfirmedZero','VerifiedAmount')))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeTaxDeclarations_LivingChildren",
                table: "EmployeeTaxDeclarations",
                sql: "[TotalLivingLawfulChildren] IS NULL OR [TotalLivingLawfulChildren]>=0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmployeeTaxClaims_ChildMetadata",
                table: "EmployeeTaxClaims",
                sql: "([ChildRelationshipType] IS NULL AND [AdditionalChildAllowanceEligible] IS NULL) OR ([ClaimType]='Child' AND [ChildRelationshipType] IS NOT NULL AND [ChildRelationshipType] IN ('Lawful','Adopted') AND [AdditionalChildAllowanceEligible] IS NOT NULL AND ([ChildRelationshipType]='Lawful' OR [AdditionalChildAllowanceEligible]=0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PitPolicyConfigurations_ClaimValues",
                table: "PitPolicyConfigurations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeTaxOpeningBalances_Scope",
                table: "EmployeeTaxOpeningBalances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeTaxDeclarations_LivingChildren",
                table: "EmployeeTaxDeclarations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmployeeTaxClaims_ChildMetadata",
                table: "EmployeeTaxClaims");

            migrationBuilder.DropColumn(
                name: "AdditionalChildAllowanceAmount",
                table: "PitPolicyConfigurations");

            migrationBuilder.DropColumn(
                name: "AdoptedChildCombinedCountLimit",
                table: "PitPolicyConfigurations");

            migrationBuilder.DropColumn(
                name: "ChildAllowanceAmount",
                table: "PitPolicyConfigurations");

            migrationBuilder.DropColumn(
                name: "MaximumEligibleParentCount",
                table: "PitPolicyConfigurations");

            migrationBuilder.DropColumn(
                name: "ParentAllowanceAmount",
                table: "PitPolicyConfigurations");

            migrationBuilder.DropColumn(
                name: "SpouseAllowanceAmount",
                table: "PitPolicyConfigurations");

            migrationBuilder.DropColumn(
                name: "CompletenessAttested",
                table: "EmployeeTaxOpeningBalances");

            migrationBuilder.DropColumn(
                name: "InputContractVersion",
                table: "EmployeeTaxOpeningBalances");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceScope",
                table: "EmployeeTaxOpeningBalances");

            migrationBuilder.DropColumn(
                name: "TotalLivingLawfulChildren",
                table: "EmployeeTaxDeclarations");

            migrationBuilder.DropColumn(
                name: "AdditionalChildAllowanceEligible",
                table: "EmployeeTaxClaims");

            migrationBuilder.DropColumn(
                name: "ChildRelationshipType",
                table: "EmployeeTaxClaims");
        }
    }
}
