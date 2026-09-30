using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRuleTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollRuleTargets",
                columns: table => new
                {
                    PayrollRuleTargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsExcluded = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRuleTargets", x => x.PayrollRuleTargetId);
                    table.CheckConstraint("CK_PayrollRuleTargets_TargetType", "[TargetType] IN ('Employee', 'Department', 'Designation', 'EmploymentType', 'Location')");
                    table.ForeignKey(
                        name: "FK_PayrollRuleTargets_PayrollRules_PayrollRuleId",
                        column: x => x.PayrollRuleId,
                        principalTable: "PayrollRules",
                        principalColumn: "PayrollRuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuleTargets_PayrollRuleId",
                table: "PayrollRuleTargets",
                column: "PayrollRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuleTargets_TargetType_TargetId",
                table: "PayrollRuleTargets",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "UX_PayrollRuleTargets_Rule_Type_Target",
                table: "PayrollRuleTargets",
                columns: new[] { "PayrollRuleId", "TargetType", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollRuleTargets");
        }
    }
}
