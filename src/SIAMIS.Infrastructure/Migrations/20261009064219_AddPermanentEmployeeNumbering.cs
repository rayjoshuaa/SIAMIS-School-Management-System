using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermanentEmployeeNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence(
                name: "EmployeeNumberSequence",
                schema: "dbo",
                startValue: 100000L,
                minValue: 100000L,
                maxValue: 9223372036854775806L);

            migrationBuilder.CreateTable(
                name: "EmployeeNumberReservations",
                columns: table => new
                {
                    EmployeeNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeNumberReservations", x => x.EmployeeNumber);
                    table.CheckConstraint("CK_EmployeeNumberReservations_State", "([AssignedAtUtc] IS NULL AND [RetiredAtUtc] IS NULL) OR ([AssignedAtUtc] IS NOT NULL AND ([RetiredAtUtc] IS NULL OR [RetiredAtUtc] >= [AssignedAtUtc]))");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNumberReservations_EmployeeId",
                table: "EmployeeNumberReservations",
                column: "EmployeeId",
                unique: true);
            // One-time compatibility preflight, never the runtime allocation strategy.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM dbo.Employees WHERE TRY_CONVERT(decimal(38,0), EmployeeNumber) >= 9223372036854775806)
                    THROW 51000, 'Existing numeric employee identifiers exceed allocator capacity. Review before migration.', 1;
                INSERT dbo.EmployeeNumberReservations (EmployeeNumber, EmployeeId, ReservedAtUtc, AssignedAtUtc)
                    SELECT EmployeeNumber, EmployeeId, SYSUTCDATETIME(), SYSUTCDATETIME() FROM dbo.Employees;
                DECLARE @last bigint = (SELECT TOP (1) TRY_CONVERT(bigint, EmployeeNumber) FROM dbo.Employees
                    WHERE TRY_CONVERT(bigint, EmployeeNumber) >= 100000 ORDER BY TRY_CONVERT(bigint, EmployeeNumber) DESC);
                IF @last IS NOT NULL
                BEGIN
                    DECLARE @restart nvarchar(200) = N'ALTER SEQUENCE dbo.EmployeeNumberSequence RESTART WITH ' + CONVERT(nvarchar(30), @last + 1);
                    EXEC sys.sp_executesql @restart;
                END;
                """);
            migrationBuilder.Sql(SqlDefinition("""
                CREATE PROCEDURE dbo.ReserveEmployeeNumber @EmployeeId uniqueidentifier
                AS
                BEGIN
                    SET NOCOUNT ON; SET XACT_ABORT ON;
                    IF @@TRANCOUNT <> 0 THROW 51001, 'Reservation requires its own committed connection.', 1;
                    DECLARE @number nvarchar(30) = CONVERT(nvarchar(30), NEXT VALUE FOR dbo.EmployeeNumberSequence);
                    BEGIN TRANSACTION;
                    INSERT dbo.EmployeeNumberReservations (EmployeeNumber, EmployeeId, ReservedAtUtc)
                        VALUES (@number, @EmployeeId, SYSUTCDATETIME());
                    COMMIT TRANSACTION;
                    SELECT @number;
                END;
                """));
            migrationBuilder.Sql(SqlDefinition("""
                CREATE TRIGGER dbo.TR_Employees_PermanentIdentity ON dbo.Employees AFTER INSERT, UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted) AND EXISTS (SELECT 1 FROM deleted)
                       AND (UPDATE(EmployeeNumber) OR UPDATE(EmployeeId))
                       AND EXISTS (SELECT EmployeeId, CONVERT(varbinary(60), EmployeeNumber) FROM deleted
                                   EXCEPT SELECT EmployeeId, CONVERT(varbinary(60), EmployeeNumber) FROM inserted)
                        THROW 51002, 'Employee GUID and number are permanent.', 1;
                    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.EmployeeId = i.EmployeeId
                        LEFT JOIN dbo.EmployeeNumberReservations r ON r.EmployeeNumber = i.EmployeeNumber AND r.EmployeeId = i.EmployeeId
                        WHERE d.EmployeeId IS NULL AND (r.EmployeeId IS NULL OR r.AssignedAtUtc IS NOT NULL OR r.RetiredAtUtc IS NOT NULL
                            OR CONVERT(varbinary(60), r.EmployeeNumber) <> CONVERT(varbinary(60), i.EmployeeNumber)))
                        THROW 51003, 'A new employee requires an unused number reservation bound to its GUID.', 1;
                    UPDATE r SET AssignedAtUtc = SYSUTCDATETIME()
                        FROM dbo.EmployeeNumberReservations r JOIN inserted i ON i.EmployeeId = r.EmployeeId
                        LEFT JOIN deleted d ON d.EmployeeId = i.EmployeeId WHERE d.EmployeeId IS NULL;
                    UPDATE r SET RetiredAtUtc = SYSUTCDATETIME()
                        FROM dbo.EmployeeNumberReservations r JOIN deleted d ON d.EmployeeId = r.EmployeeId
                        LEFT JOIN inserted i ON i.EmployeeId = d.EmployeeId WHERE i.EmployeeId IS NULL;
                END;
                """));
            migrationBuilder.Sql(SqlDefinition("""
                CREATE TRIGGER dbo.TR_EmployeeNumberReservations_Permanent ON dbo.EmployeeNumberReservations AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT EmployeeNumber, EmployeeId, ReservedAtUtc FROM deleted
                               EXCEPT SELECT EmployeeNumber, EmployeeId, ReservedAtUtc FROM inserted)
                        THROW 51004, 'Number reservations cannot be deleted or reassigned.', 1;
                    IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON i.EmployeeNumber = d.EmployeeNumber
                        WHERE CONVERT(varbinary(60), d.EmployeeNumber) <> CONVERT(varbinary(60), i.EmployeeNumber)
                        OR NOT (
                            (d.AssignedAtUtc IS NULL AND d.RetiredAtUtc IS NULL AND i.AssignedAtUtc IS NOT NULL AND i.RetiredAtUtc IS NULL
                             AND EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.EmployeeId = i.EmployeeId AND e.EmployeeNumber = i.EmployeeNumber))
                            OR
                            (d.AssignedAtUtc IS NOT NULL AND d.RetiredAtUtc IS NULL AND i.AssignedAtUtc = d.AssignedAtUtc AND i.RetiredAtUtc IS NOT NULL
                             AND NOT EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.EmployeeId = i.EmployeeId))
                        ))
                        THROW 51005, 'Only first assignment or permanent retirement is permitted.', 1;
                END;
                """));
        }

        // CREATE PROCEDURE/TRIGGER must start a batch, including in idempotent scripts.
        private static string SqlDefinition(string sql) => "EXEC(N'" + sql.Replace("'", "''") + "');";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // An automatic downgrade would erase lifetime identity protection.
            migrationBuilder.Sql("THROW 51006, 'Permanent employee numbering cannot be automatically downgraded. Preserve the reservation ledger and obtain a reviewed recovery plan.', 1;");
        }
    }
}
