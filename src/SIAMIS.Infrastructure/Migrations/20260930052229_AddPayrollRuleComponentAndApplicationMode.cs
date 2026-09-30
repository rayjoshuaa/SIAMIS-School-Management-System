using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollRuleComponentAndApplicationMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationMode",
                table: "PayrollRules",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Supplement");

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollComponentId",
                table: "PayrollRules",
                type: "uniqueidentifier",
                nullable: true);

            // Existing rules have no component association to backfill safely. Abort atomically and require
            // an explicit mapping rather than assigning an arbitrary component or creating an invalid FK.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [dbo].[PayrollRules] WHERE [PayrollComponentId] IS NULL) THROW 51000, 'Existing payroll rules require an explicit PayrollComponentId mapping before this migration can be applied.', 1;");

            migrationBuilder.AlterColumn<Guid>(
                name: "PayrollComponentId",
                table: "PayrollRules",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRules_PayrollComponentId",
                table: "PayrollRules",
                column: "PayrollComponentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollRules_ApplicationMode",
                table: "PayrollRules",
                sql: "[ApplicationMode] IN ('Supplement', 'ReplaceAssignment')");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollRules_PayrollComponents_PayrollComponentId",
                table: "PayrollRules",
                column: "PayrollComponentId",
                principalTable: "PayrollComponents",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollRules_PayrollComponents_PayrollComponentId",
                table: "PayrollRules");

            migrationBuilder.DropIndex(
                name: "IX_PayrollRules_PayrollComponentId",
                table: "PayrollRules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollRules_ApplicationMode",
                table: "PayrollRules");

            migrationBuilder.DropColumn(
                name: "ApplicationMode",
                table: "PayrollRules");

            migrationBuilder.DropColumn(
                name: "PayrollComponentId",
                table: "PayrollRules");
        }
    }
}
