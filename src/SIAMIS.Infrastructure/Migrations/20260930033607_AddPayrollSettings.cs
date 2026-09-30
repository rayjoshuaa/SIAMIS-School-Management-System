using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollSettings",
                columns: table => new
                {
                    PayrollSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PayFrequency = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PayrollCutoffDay = table.Column<int>(type: "int", nullable: false),
                    DefaultPayDay = table.Column<int>(type: "int", nullable: false),
                    WorkingDaysPerPeriod = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    WorkingHoursPerDay = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RoundingMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSettings", x => x.PayrollSettingsId);
                    table.CheckConstraint("CK_PayrollSettings_CalendarDays", "[PayrollCutoffDay] BETWEEN 1 AND 31 AND [DefaultPayDay] BETWEEN 1 AND 31");
                    table.CheckConstraint("CK_PayrollSettings_DecimalPlaces", "[DecimalPlaces] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_PayrollSettings_WorkingValues", "[WorkingDaysPerPeriod] > 0 AND [WorkingDaysPerPeriod] <= 366 AND [WorkingHoursPerDay] > 0 AND [WorkingHoursPerDay] <= 24");
                });

            migrationBuilder.CreateIndex(
                name: "UX_PayrollSettings_OneActive",
                table: "PayrollSettings",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollSettings");
        }
    }
}
