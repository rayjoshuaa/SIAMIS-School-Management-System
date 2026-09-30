using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePayrollComponentAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePayrollComponentAssignments",
                columns: table => new
                {
                    EmployeePayrollComponentAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollComponentAssignments", x => x.EmployeePayrollComponentAssignmentId);
                    table.CheckConstraint("CK_EmployeePayrollComponentAssignments_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_EmployeePayrollComponentAssignments_NonNegativeValues", "[Amount] >= 0 AND ([Quantity] IS NULL OR [Quantity] >= 0) AND ([Rate] IS NULL OR [Rate] >= 0)");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollComponentAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollComponentAssignments_PayrollComponents_PayrollComponentId",
                        column: x => x.PayrollComponentId,
                        principalTable: "PayrollComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollComponentAssignments_Employee_Component_EffectiveFrom",
                table: "EmployeePayrollComponentAssignments",
                columns: new[] { "EmployeeId", "PayrollComponentId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollComponentAssignments_Employee_EffectiveFrom",
                table: "EmployeePayrollComponentAssignments",
                columns: new[] { "EmployeeId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollComponentAssignments_EmployeeId",
                table: "EmployeePayrollComponentAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollComponentAssignments_PayrollComponentId",
                table: "EmployeePayrollComponentAssignments",
                column: "PayrollComponentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayrollComponentAssignments");
        }
    }
}
