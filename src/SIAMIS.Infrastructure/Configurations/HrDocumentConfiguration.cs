using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class HrDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> b)
    {
        b.Property(x=>x.Category).HasMaxLength(40).HasDefaultValue("GeneralHRDocument");
        b.Property(x=>x.ContentType).HasMaxLength(40);
        b.Property(x=>x.ContentSha256).HasMaxLength(64).IsUnicode(false);
        b.Property(x=>x.LifecycleStatus).HasMaxLength(20).HasDefaultValue("MetadataOnly");
        // Existing metadata receives a migration-time random version, never a fabricated content hash.
        b.Property(x=>x.Version).HasMaxLength(36).IsConcurrencyToken().HasDefaultValueSql("CONVERT(varchar(36),NEWID())");
        b.Property(x=>x.UploadedAt).HasColumnType("datetime2(7)");
        b.Property(x=>x.LifecycleChangedAtUtc).HasColumnType("datetime2(7)");
        b.HasIndex(x=>new{x.EmployeeId,x.LifecycleStatus});
        b.HasIndex(x=>x.SupersedesDocumentId).IsUnique().HasFilter("[SupersedesDocumentId] IS NOT NULL");
        b.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x=>new{x.EmployeeId,x.SupersedesDocumentId})
            .HasPrincipalKey(x=>new{x.EmployeeId,x.EmployeeDocumentId}).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmploymentRecord>().WithMany().HasForeignKey(x=>new{x.EmployeeId,x.EmploymentRecordId})
            .HasPrincipalKey(x=>new{x.EmployeeId,x.EmploymentRecordId}).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EmployeeLeaveEvidence>().WithMany().HasForeignKey(x=>new{x.EmployeeId,x.LeaveId,x.LeaveEvidenceId})
            .HasPrincipalKey(x=>new{x.EmployeeId,x.LeaveId,x.Id}).OnDelete(DeleteBehavior.NoAction);
        b.ToTable("EmployeeDocuments",t=>{
            t.HasCheckConstraint("CK_HrDocument_Category","[Category] IN ('EmploymentContract','Identification','WorkAuthorization','QualificationOrCertificate','GeneralHRDocument')");
            t.HasCheckConstraint("CK_HrDocument_Lifecycle","[LifecycleStatus] IN ('MetadataOnly','Active','Superseded','Archived')");
            t.HasCheckConstraint("CK_HrDocument_Content","([ContentSha256] IS NULL AND [ContentType] IS NULL AND [SizeBytes] IS NULL) OR ([ContentSha256] IS NOT NULL AND [ContentType] IS NOT NULL AND [SizeBytes] IS NOT NULL AND LEN([ContentSha256])=64 AND [ContentType] IN ('application/pdf','image/jpeg','image/png') AND [SizeBytes]>0 AND [SizeBytes]<=20971520)");
            t.HasCheckConstraint("CK_HrDocument_LeaveAssociation","([LeaveId] IS NULL AND [LeaveEvidenceId] IS NULL) OR ([LeaveId] IS NOT NULL AND [LeaveEvidenceId] IS NOT NULL)");
            t.HasCheckConstraint("CK_HrDocument_Successor","[SupersedesDocumentId] IS NULL OR [SupersedesDocumentId]<>[EmployeeDocumentId]");
        });
    }
}
