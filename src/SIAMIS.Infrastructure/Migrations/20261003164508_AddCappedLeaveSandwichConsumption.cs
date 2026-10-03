using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCappedLeaveSandwichConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliedDebitMinutes",
                table: "EmployeeLeaveSandwichAllocations",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveSandwichAllocation_Applied",
                table: "EmployeeLeaveSandwichAllocations",
                sql: "[AppliedDebitMinutes] IS NULL OR ([AppliedDebitMinutes] >= 0 AND [AppliedDebitMinutes] <= [SandwichDebitMinutes])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveSandwichAllocation_Applied",
                table: "EmployeeLeaveSandwichAllocations");

            migrationBuilder.DropColumn(
                name: "AppliedDebitMinutes",
                table: "EmployeeLeaveSandwichAllocations");
        }
    }
}
