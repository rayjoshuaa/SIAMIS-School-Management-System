using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveSandwichReviewLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveSandwichEvent_State",
                table: "EmployeeLeaveSandwichEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveSandwich_State",
                table: "EmployeeLeaveSandwichCases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveSandwichEvent_State",
                table: "EmployeeLeaveSandwichEvents",
                sql: "[State] IN ('Reserved','Charged','Exempted','Released','ReviewPending','ReasonAccepted','ReasonNotAccepted')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveSandwich_State",
                table: "EmployeeLeaveSandwichCases",
                sql: "[State] IN ('Reserved','Charged','Exempted','Released','ReviewPending','ReasonAccepted','ReasonNotAccepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveSandwichEvent_State",
                table: "EmployeeLeaveSandwichEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveSandwich_State",
                table: "EmployeeLeaveSandwichCases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveSandwichEvent_State",
                table: "EmployeeLeaveSandwichEvents",
                sql: "[State] IN ('Reserved','Charged','Exempted','Released')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveSandwich_State",
                table: "EmployeeLeaveSandwichCases",
                sql: "[State] IN ('Reserved','Charged','Exempted','Released')");
        }
    }
}
