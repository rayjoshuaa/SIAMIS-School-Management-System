using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHrIdentityAndSecurityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceReviewAction_Shape",
                table: "AttendanceReviewActions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents");

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPasswordChange = table.Column<bool>(type: "bit", nullable: false),
                    AdministrationVersion = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("d1000000-0000-0000-0000-000000000001"), "d1000000-0000-0000-0000-000000000001", "SystemAdmin", "SYSTEMADMIN" },
                    { new Guid("d1000000-0000-0000-0000-000000000002"), "d1000000-0000-0000-0000-000000000002", "HRAdmin", "HRADMIN" },
                    { new Guid("d1000000-0000-0000-0000-000000000003"), "d1000000-0000-0000-0000-000000000003", "PayrollAdmin", "PAYROLLADMIN" },
                    { new Guid("d1000000-0000-0000-0000-000000000004"), "d1000000-0000-0000-0000-000000000004", "Management", "MANAGEMENT" },
                    { new Guid("d1000000-0000-0000-0000-000000000005"), "d1000000-0000-0000-0000-000000000005", "Employee", "EMPLOYEE" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceReviewAction_Shape",
                table: "AttendanceReviewActions",
                sql: "[Sequence] > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND LEN([SourceFingerprint]) = 64 AND ISJSON([CalculationJson]) = 1 AND (([Origin] = 'DevelopmentUnattributed' AND [ActorUserId] IS NULL) OR ([Origin] = 'Authenticated' AND [ActorUserId] IS NOT NULL)) AND (([Action] IN ('CorrectionAdded','Excluded','Included') AND [AttendanceEventId] IS NOT NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'AbsenceConfirmed' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'Finalized' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL) OR ([Action] = 'Reopened' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL AND [ReviewCaseId] IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents",
                sql: "([Source] = 'ManualAuthorized' AND [ManualRequestKey] IS NOT NULL AND [ManualRequestKey] <> '00000000-0000-0000-0000-000000000000' AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [SourceKey] IS NULL AND [ExternalEventId] IS NULL) OR ([Source] IN ('Device','Imported') AND [SourceKey] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceKey]))) > 0 AND [ExternalEventId] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalEventId]))) > 0 AND [ManualRequestKey] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Roles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_OccurredAtUtc",
                table: "SecurityAuditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmployeeId",
                table: "Users",
                column: "EmployeeId",
                unique: true,
                filter: "[EmployeeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "Users",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "SecurityAuditEvents");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceReviewAction_Shape",
                table: "AttendanceReviewActions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceReviewAction_Shape",
                table: "AttendanceReviewActions",
                sql: "[Sequence] > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND LEN([SourceFingerprint]) = 64 AND ISJSON([CalculationJson]) = 1 AND [Origin] = 'DevelopmentUnattributed' AND (([Action] IN ('CorrectionAdded','Excluded','Included') AND [AttendanceEventId] IS NOT NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'AbsenceConfirmed' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'Finalized' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL) OR ([Action] = 'Reopened' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL AND [ReviewCaseId] IS NOT NULL))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceEvent_SourceFields",
                table: "AttendanceEvents",
                sql: "([Source] = 'ManualAuthorized' AND [ManualRequestKey] IS NOT NULL AND [ManualRequestKey] <> '00000000-0000-0000-0000-000000000000' AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [SourceKey] IS NULL AND [ExternalEventId] IS NULL AND [ActorId] IS NULL) OR ([Source] IN ('Device','Imported') AND [SourceKey] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceKey]))) > 0 AND [ExternalEventId] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalEventId]))) > 0 AND [ManualRequestKey] IS NULL)");
        }
    }
}
