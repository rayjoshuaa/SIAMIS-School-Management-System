using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class AttendanceReviewCaseConfiguration : IEntityTypeConfiguration<AttendanceReviewCase>
{
    public void Configure(EntityTypeBuilder<AttendanceReviewCase> b)
    {
        b.ToTable("AttendanceReviewCases", t =>
        {
            t.HasCheckConstraint("CK_AttendanceReviewCase_State", "[State] IN ('Open','Resolved')");
            t.HasCheckConstraint("CK_AttendanceReviewCase_Snapshot", "ISJSON([OriginalCalculationJson]) = 1 AND LEN([OriginalSourceFingerprint]) = 64");
        });
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.EmployeeId, x.BusinessDate }).IsUnique();
        b.HasAlternateKey(x => new { x.EmployeeId, x.BusinessDate, x.Id });
        b.Property(x => x.BusinessDate).HasColumnType("date"); b.Property(x => x.OpenedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.State).HasMaxLength(20).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.OriginalSourceFingerprint).HasMaxLength(64).IsRequired();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class AttendanceReviewActionConfiguration : IEntityTypeConfiguration<AttendanceReviewAction>
{
    public void Configure(EntityTypeBuilder<AttendanceReviewAction> b)
    {
        b.ToTable("AttendanceReviewActions", t =>
        {
            t.HasCheckConstraint("CK_AttendanceReviewAction_Shape", "[Sequence] > 0 AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND LEN([SourceFingerprint]) = 64 AND ISJSON([CalculationJson]) = 1 AND [Origin] = 'DevelopmentUnattributed' AND (([Action] IN ('CorrectionAdded','Excluded','Included') AND [AttendanceEventId] IS NOT NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'AbsenceConfirmed' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NULL AND [ReviewCaseId] IS NOT NULL) OR ([Action] = 'Finalized' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL) OR ([Action] = 'Reopened' AND [AttendanceEventId] IS NULL AND [FinalizedRevisionId] IS NOT NULL AND [ReviewCaseId] IS NOT NULL))");
        });
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.EmployeeId, x.BusinessDate, x.Sequence }).IsUnique();
        b.HasIndex(x => x.FinalizedRevisionId).IsUnique().HasFilter("[Action] = 'Reopened'");
        b.Property(x => x.BusinessDate).HasColumnType("date"); b.Property(x => x.OccurredAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.Action).HasMaxLength(30).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Origin).HasMaxLength(40); b.Property(x => x.Reason).HasMaxLength(2000); b.Property(x => x.SourceFingerprint).HasMaxLength(64);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AttendanceReviewCase>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.BusinessDate, x.ReviewCaseId }).HasPrincipalKey(x => new { x.EmployeeId, x.BusinessDate, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<FinalizedAttendanceRevision>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.BusinessDate, x.FinalizedRevisionId }).HasPrincipalKey(x => new { x.EmployeeId, x.BusinessDate, x.Id }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AttendanceEvent>().WithMany().HasForeignKey(x => x.AttendanceEventId).OnDelete(DeleteBehavior.NoAction);
    }
}
internal sealed class FinalizedAttendanceRevisionConfiguration : IEntityTypeConfiguration<FinalizedAttendanceRevision>
{
    public void Configure(EntityTypeBuilder<FinalizedAttendanceRevision> b)
    {
        b.ToTable("FinalizedAttendanceRevisions", t =>
        {
            t.HasCheckConstraint("CK_FinalizedAttendanceRevision_Snapshot", "[Revision] > 0 AND LEN([SourceFingerprint]) = 64 AND ISJSON([SourcesJson]) = 1 AND ISJSON([SnapshotJson]) = 1");
            t.HasCheckConstraint("CK_FinalizedAttendanceRevision_Coverage", "[ScheduledMilliseconds] >= 0 AND [PresenceCoveredScheduledMilliseconds] >= 0 AND [ApprovedLeaveCoveredScheduledMilliseconds] >= 0 AND [UnexplainedScheduledMilliseconds] >= 0 AND [CoverageTruncationResidualMilliseconds] >= 0 AND [ScheduledMilliseconds] = [PresenceCoveredScheduledMilliseconds] + [ApprovedLeaveCoveredScheduledMilliseconds] + [UnexplainedScheduledMilliseconds] + [CoverageTruncationResidualMilliseconds] AND ([IsConfirmedAbsent] = 0 OR ([ScheduledMilliseconds] > 0 AND [PresenceCoveredScheduledMilliseconds] = 0 AND [ApprovedLeaveCoveredScheduledMilliseconds] = 0))");
        });
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.EmployeeId, x.BusinessDate, x.Revision }).IsUnique();
        b.HasAlternateKey(x => new { x.EmployeeId, x.BusinessDate, x.Id });
        b.Property(x => x.BusinessDate).HasColumnType("date"); b.Property(x => x.FinalizedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.SourceFingerprint).HasMaxLength(64);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<AttendanceReviewCase>().WithMany().HasForeignKey(x => new { x.EmployeeId, x.BusinessDate, x.ReviewCaseId }).HasPrincipalKey(x => new { x.EmployeeId, x.BusinessDate, x.Id }).OnDelete(DeleteBehavior.NoAction);
    }
}
