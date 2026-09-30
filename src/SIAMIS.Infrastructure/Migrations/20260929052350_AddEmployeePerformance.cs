using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePerformance",
                columns: table => new
                {
                    PerformanceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewPeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewPeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    PerformanceRatingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Strengths = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AreasForImprovement = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Goals = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePerformance", x => x.PerformanceRecordId);
                    table.ForeignKey(
                        name: "FK_EmployeePerformance_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeePerformance_Employees_ReviewerEmployeeId",
                        column: x => x.ReviewerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_EmployeePerformance_PerformanceRatings_PerformanceRatingId",
                        column: x => x.PerformanceRatingId,
                        principalTable: "PerformanceRatings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePerformance_EmployeeId_ReviewDate",
                table: "EmployeePerformance",
                columns: new[] { "EmployeeId", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePerformance_PerformanceRatingId",
                table: "EmployeePerformance",
                column: "PerformanceRatingId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePerformance_ReviewerEmployeeId",
                table: "EmployeePerformance",
                column: "ReviewerEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePerformance");
        }
    }
}
