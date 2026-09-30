using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attendance",
                columns: table => new
                {
                    AttendanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AttendanceStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckIn = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    CheckOut = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_Attendance_AttendanceStatuses_AttendanceStatusId",
                        column: x => x.AttendanceStatusId,
                        principalTable: "AttendanceStatuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Attendance_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_AttendanceStatusId",
                table: "Attendance",
                column: "AttendanceStatusId");

            migrationBuilder.CreateIndex(
                name: "UX_Attendance_EmployeeId_AttendanceDate",
                table: "Attendance",
                columns: new[] { "EmployeeId", "AttendanceDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attendance");
        }
    }
}
