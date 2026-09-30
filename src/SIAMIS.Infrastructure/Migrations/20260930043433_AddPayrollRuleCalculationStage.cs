using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRuleCalculationStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalculationStage",
                table: "PayrollRules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Earning");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalculationStage",
                table: "PayrollRules");
        }
    }
}
