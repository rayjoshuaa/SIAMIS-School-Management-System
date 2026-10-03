using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollOperationsAndPayslips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePayslips",
                columns: table => new
                {
                    EmployeePayslipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeePayrollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotVersion = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePayslips", x => x.EmployeePayslipId);
                    table.CheckConstraint("CK_EmployeePayslips_Json", "ISJSON([SnapshotJson]) = 1");
                    table.CheckConstraint("CK_EmployeePayslips_Version", "[SnapshotVersion] = 1");
                    table.ForeignKey(
                        name: "FK_EmployeePayslips_EmployeePayrolls_EmployeePayrollId",
                        column: x => x.EmployeePayrollId,
                        principalTable: "EmployeePayrolls",
                        principalColumn: "EmployeePayrollId");
                });

            migrationBuilder.CreateTable(
                name: "OrganizationProfiles",
                columns: table => new
                {
                    OrganizationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProfiles", x => x.OrganizationProfileId);
                    table.CheckConstraint("CK_OrganizationProfiles_DisplayName", "LEN(LTRIM(RTRIM([DisplayName]))) > 0");
                    table.CheckConstraint("CK_OrganizationProfiles_Singleton", "[OrganizationProfileId] = '00000000-0000-0000-0000-000000000001'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayslips_EmployeePayrollId",
                table: "EmployeePayslips",
                column: "EmployeePayrollId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayslips");

            migrationBuilder.DropTable(
                name: "OrganizationProfiles");
        }
    }
}
