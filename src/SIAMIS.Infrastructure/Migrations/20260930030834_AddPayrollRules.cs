using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollRules",
                columns: table => new
                {
                    PayrollRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RuleType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CalculationMethod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    FixedAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    MinimumBase = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    MaximumBase = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    BaseType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AppliesTo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRules", x => x.PayrollRuleId);
                    table.CheckConstraint("CK_PayrollRules_BaseRange", "[MinimumBase] IS NULL OR [MaximumBase] IS NULL OR [MaximumBase] >= [MinimumBase]");
                    table.CheckConstraint("CK_PayrollRules_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_PayrollRules_NonNegativeValues", "([Rate] IS NULL OR [Rate] >= 0) AND ([FixedAmount] IS NULL OR [FixedAmount] >= 0) AND ([MinimumBase] IS NULL OR [MinimumBase] >= 0) AND ([MaximumBase] IS NULL OR [MaximumBase] >= 0)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRules_EffectiveFrom",
                table: "PayrollRules",
                column: "EffectiveFrom");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRules_IsActive",
                table: "PayrollRules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRules_RuleType",
                table: "PayrollRules",
                column: "RuleType");

            migrationBuilder.CreateIndex(
                name: "UX_PayrollRules_Code",
                table: "PayrollRules",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollRules");
        }
    }
}
