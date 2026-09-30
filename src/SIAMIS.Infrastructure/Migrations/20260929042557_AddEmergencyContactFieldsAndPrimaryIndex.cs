using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyContactFieldsAndPrimaryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "EmergencyContacts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AlternativePhone",
                table: "EmergencyContacts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_EmergencyContacts_PrimaryPerEmployee",
                table: "EmergencyContacts",
                column: "EmployeeId",
                unique: true,
                filter: "[IsPrimary] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_EmergencyContacts_PrimaryPerEmployee",
                table: "EmergencyContacts");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "EmergencyContacts");

            migrationBuilder.DropColumn(
                name: "AlternativePhone",
                table: "EmergencyContacts");
        }
    }
}
