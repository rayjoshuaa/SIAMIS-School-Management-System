using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePayrolls",
                columns: table => new
                {
                    EmployeePayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BasicSalary = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrolls", x => x.EmployeePayrollId);
                    table.CheckConstraint("CK_EmployeePayrolls_NonNegativeTotals", "[BasicSalary] >= 0 AND [GrossPay] >= 0 AND [TotalDeductions] >= 0 AND [NetPay] >= 0");
                    table.CheckConstraint("CK_EmployeePayrolls_Status", "[Status] IN ('Draft', 'Calculated', 'Approved', 'Paid', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_EmployeePayrolls_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrolls_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "PayrollPeriodId");
                });

            migrationBuilder.CreateTable(
                name: "EmployeePayrollLines",
                columns: table => new
                {
                    EmployeePayrollLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeePayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollComponentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComponentCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ComponentName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ComponentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayrollLines", x => x.EmployeePayrollLineId);
                    table.CheckConstraint("CK_EmployeePayrollLines_AmountPositive", "[Amount] > 0");
                    table.CheckConstraint("CK_EmployeePayrollLines_ComponentType", "[ComponentType] IN ('Earning', 'Deduction')");
                    table.CheckConstraint("CK_EmployeePayrollLines_QuantityNonNegative", "[Quantity] IS NULL OR [Quantity] >= 0");
                    table.CheckConstraint("CK_EmployeePayrollLines_RateNonNegative", "[Rate] IS NULL OR [Rate] >= 0");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollLines_EmployeePayrolls_EmployeePayrollId",
                        column: x => x.EmployeePayrollId,
                        principalTable: "EmployeePayrolls",
                        principalColumn: "EmployeePayrollId");
                    table.ForeignKey(
                        name: "FK_EmployeePayrollLines_PayrollComponents_PayrollComponentId",
                        column: x => x.PayrollComponentId,
                        principalTable: "PayrollComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollLines_EmployeePayrollId",
                table: "EmployeePayrollLines",
                column: "EmployeePayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollLines_PayrollComponentId",
                table: "EmployeePayrollLines",
                column: "PayrollComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrolls_EmployeeId",
                table: "EmployeePayrolls",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrolls_PayrollPeriodId",
                table: "EmployeePayrolls",
                column: "PayrollPeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrolls_Status",
                table: "EmployeePayrolls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_EmployeePayroll_PeriodEmployee",
                table: "EmployeePayrolls",
                columns: new[] { "PayrollPeriodId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayrollLines");

            migrationBuilder.DropTable(
                name: "EmployeePayrolls");
        }
    }
}
