using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAMIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecureHrDocumentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UploadedAt",
                table: "EmployeeDocuments",
                type: "datetime2(7)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "EmployeeDocuments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "GeneralHRDocument");

            migrationBuilder.AddColumn<string>(
                name: "ContentSha256",
                table: "EmployeeDocuments",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "EmployeeDocuments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "EmployeeDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EmploymentRecordId",
                table: "EmployeeDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaveEvidenceId",
                table: "EmployeeDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaveId",
                table: "EmployeeDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LifecycleChangedAtUtc",
                table: "EmployeeDocuments",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LifecycleChangedByUserId",
                table: "EmployeeDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "EmployeeDocuments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MetadataOnly");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "EmployeeDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesDocumentId",
                table: "EmployeeDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "EmployeeDocuments",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                defaultValueSql: "CONVERT(varchar(36),NEWID())");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EmploymentRecords_EmployeeId_EmploymentRecordId",
                table: "EmploymentRecords",
                columns: new[] { "EmployeeId", "EmploymentRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmploymentRecordId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmploymentRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_LeaveId_LeaveEvidenceId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "LeaveId", "LeaveEvidenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_LifecycleStatus",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "LifecycleStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_SupersedesDocumentId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "SupersedesDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_SupersedesDocumentId",
                table: "EmployeeDocuments",
                column: "SupersedesDocumentId",
                unique: true,
                filter: "[SupersedesDocumentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HrDocument_Category",
                table: "EmployeeDocuments",
                sql: "[Category] IN ('EmploymentContract','Identification','WorkAuthorization','QualificationOrCertificate','GeneralHRDocument')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HrDocument_Content",
                table: "EmployeeDocuments",
                sql: "([ContentSha256] IS NULL AND [ContentType] IS NULL AND [SizeBytes] IS NULL) OR ([ContentSha256] IS NOT NULL AND [ContentType] IS NOT NULL AND [SizeBytes] IS NOT NULL AND LEN([ContentSha256])=64 AND [ContentType] IN ('application/pdf','image/jpeg','image/png') AND [SizeBytes]>0 AND [SizeBytes]<=20971520)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HrDocument_LeaveAssociation",
                table: "EmployeeDocuments",
                sql: "([LeaveId] IS NULL AND [LeaveEvidenceId] IS NULL) OR ([LeaveId] IS NOT NULL AND [LeaveEvidenceId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HrDocument_Lifecycle",
                table: "EmployeeDocuments",
                sql: "[LifecycleStatus] IN ('MetadataOnly','Active','Superseded','Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HrDocument_Successor",
                table: "EmployeeDocuments",
                sql: "[SupersedesDocumentId] IS NULL OR [SupersedesDocumentId]<>[EmployeeDocumentId]");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeDocuments_EmployeeId_SupersedesDocumentId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "SupersedesDocumentId" },
                principalTable: "EmployeeDocuments",
                principalColumns: new[] { "EmployeeId", "EmployeeDocumentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmployeeLeaveEvidence_EmployeeId_LeaveId_LeaveEvidenceId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "LeaveId", "LeaveEvidenceId" },
                principalTable: "EmployeeLeaveEvidence",
                principalColumns: new[] { "EmployeeId", "LeaveId", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDocuments_EmploymentRecords_EmployeeId_EmploymentRecordId",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "EmploymentRecordId" },
                principalTable: "EmploymentRecords",
                principalColumns: new[] { "EmployeeId", "EmploymentRecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeDocuments_EmployeeId_SupersedesDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmployeeLeaveEvidence_EmployeeId_LeaveId_LeaveEvidenceId",
                table: "EmployeeDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeDocuments_EmploymentRecords_EmployeeId_EmploymentRecordId",
                table: "EmployeeDocuments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EmploymentRecords_EmployeeId_EmploymentRecordId",
                table: "EmploymentRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_EmploymentRecordId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_LeaveId_LeaveEvidenceId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_LifecycleStatus",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId_SupersedesDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_SupersedesDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HrDocument_Category",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HrDocument_Content",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HrDocument_LeaveAssociation",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HrDocument_Lifecycle",
                table: "EmployeeDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HrDocument_Successor",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ContentSha256",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "EmploymentRecordId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LeaveEvidenceId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LeaveId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LifecycleChangedAtUtc",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LifecycleChangedByUserId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "SupersedesDocumentId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EmployeeDocuments");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UploadedAt",
                table: "EmployeeDocuments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)");
        }
    }
}
