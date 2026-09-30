using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmploymentLifecycleIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTerminal",
                table: "EmploymentStatuses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "EmploymentStatuses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000005"),
                column: "IsTerminal",
                value: true);

            migrationBuilder.UpdateData(
                table: "EmploymentStatuses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000006"),
                column: "IsTerminal",
                value: true);

            migrationBuilder.UpdateData(
                table: "EmploymentStatuses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000007"),
                column: "IsTerminal",
                value: true);

            migrationBuilder.UpdateData(
                table: "EmploymentStatuses",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000008"),
                column: "IsTerminal",
                value: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmploymentRecords_CurrentOpen",
                table: "EmploymentRecords",
                sql: "[IsCurrent] = 0 OR [EndDate] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmploymentRecords_EndDate",
                table: "EmploymentRecords",
                sql: "[EndDate] IS NULL OR [EndDate] >= COALESCE([StartDate], [HireDate])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmploymentRecords_StartDate",
                table: "EmploymentRecords",
                sql: "[StartDate] IS NULL OR [StartDate] >= [HireDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EmploymentRecords_CurrentOpen",
                table: "EmploymentRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmploymentRecords_EndDate",
                table: "EmploymentRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmploymentRecords_StartDate",
                table: "EmploymentRecords");

            migrationBuilder.DropColumn(
                name: "IsTerminal",
                table: "EmploymentStatuses");
        }
    }
}
