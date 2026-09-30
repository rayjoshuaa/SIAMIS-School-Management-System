using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRulePriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "PayrollRules",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "PayrollRules");
        }
    }
}
