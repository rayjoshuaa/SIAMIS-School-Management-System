using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateEmployeePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePhotoRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(37)", maxLength: 37, nullable: true),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePhotoRevisions", x => x.Id);
                    table.CheckConstraint("CK_EmployeePhoto_Binary", "([Operation]='Remove' AND [StorageKey] IS NULL AND [Sha256] IS NULL AND [ContentType] IS NULL AND [SizeBytes] IS NULL AND [Width] IS NULL AND [Height] IS NULL) OR ([Operation] IN ('Upload','Replace') AND [StorageKey] IS NOT NULL AND [Sha256] IS NOT NULL AND [ContentType] IS NOT NULL AND [SizeBytes] IS NOT NULL AND [Width] IS NOT NULL AND [Height] IS NOT NULL AND [ContentType] IN ('image/jpeg','image/png') AND [SizeBytes] > 0 AND [SizeBytes] <= 5242880 AND [Width] BETWEEN 1 AND 4096 AND [Height] BETWEEN 1 AND 4096)");
                    table.ForeignKey(
                        name: "FK_EmployeePhotoRevisions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePhotoRevisions_EmployeeId",
                table: "EmployeePhotoRevisions",
                column: "EmployeeId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePhotoRevisions_EmployeeId_CreatedAtUtc",
                table: "EmployeePhotoRevisions",
                columns: new[] { "EmployeeId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [EmployeePhotoRevisions]) THROW 51010, 'Photo history exists. Export and approve retention handling before schema rollback.', 1;");
            migrationBuilder.DropTable(
                name: "EmployeePhotoRevisions");
        }
    }
}
