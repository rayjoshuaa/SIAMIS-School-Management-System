using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeRegistrationIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeRegistrationReceipts",
                columns: table => new
                {
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "varbinary(32)", nullable: false),
                    ResultEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResultEmployeeNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ReplayUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeRegistrationReceipts", x => new { x.ActorUserId, x.Operation, x.RequestKey });
                    table.CheckConstraint("CK_EmployeeRegistrationReceipt_Hash", "DATALENGTH([PayloadHash]) = 32");
                    table.CheckConstraint("CK_EmployeeRegistrationReceipt_Identity", "[ActorUserId] <> '00000000-0000-0000-0000-000000000000' AND [RequestKey] <> '00000000-0000-0000-0000-000000000000' AND [ResultEmployeeId] <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_EmployeeRegistrationReceipt_Operation", "[Operation] = N'Employee.Register.v1'");
                    table.CheckConstraint("CK_EmployeeRegistrationReceipt_ReplayWindow", "[ReplayUntilUtc] > [CompletedAtUtc]");
                    table.CheckConstraint("CK_EmployeeRegistrationReceipt_Response", "[ResponseJson] IS NULL OR ISJSON([ResponseJson]) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRegistrationReceipts_ReplayUntilUtc",
                table: "EmployeeRegistrationReceipts",
                column: "ReplayUntilUtc",
                filter: "[ResponseJson] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRegistrationReceipts_ResultEmployeeId",
                table: "EmployeeRegistrationReceipts",
                column: "ResultEmployeeId",
                unique: true);

            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER dbo.TR_EmployeeRegistrationReceipts_Permanent
                ON dbo.EmployeeRegistrationReceipts AFTER UPDATE, DELETE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM deleted d
                        LEFT JOIN inserted i ON i.ActorUserId=d.ActorUserId
                            AND i.Operation=d.Operation AND i.RequestKey=d.RequestKey
                        WHERE i.RequestKey IS NULL
                            OR i.PayloadHash<>d.PayloadHash
                            OR i.ResultEmployeeId<>d.ResultEmployeeId
                            OR i.ResultEmployeeNumber COLLATE Latin1_General_100_BIN2<>d.ResultEmployeeNumber COLLATE Latin1_General_100_BIN2
                            OR DATALENGTH(i.ResultEmployeeNumber)<>DATALENGTH(d.ResultEmployeeNumber)
                            OR i.CompletedAtUtc<>d.CompletedAtUtc
                            OR i.ReplayUntilUtc<>d.ReplayUntilUtc
                            OR d.ResponseJson IS NULL OR i.ResponseJson IS NOT NULL
                            OR d.ReplayUntilUtc>SYSUTCDATETIME()
                    ) THROW 51030, ''Registration receipts are permanent; only expired response removal is allowed.'', 1;
                END');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM dbo.EmployeeRegistrationReceipts) THROW 51031, 'Registration receipts exist. Removing replay protection requires an explicitly approved recovery plan.', 1;");
            migrationBuilder.DropTable(
                name: "EmployeeRegistrationReceipts");
        }
    }
}
