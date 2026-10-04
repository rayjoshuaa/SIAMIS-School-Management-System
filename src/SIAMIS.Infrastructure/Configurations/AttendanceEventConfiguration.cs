using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Configurations;

internal sealed class AttendanceEventConfiguration : IEntityTypeConfiguration<AttendanceEvent>
{
    public void Configure(EntityTypeBuilder<AttendanceEvent> b)
    {
        b.ToTable("AttendanceEvents", t =>
        {
            t.HasCheckConstraint("CK_AttendanceEvent_Direction", "[Direction] IN ('In','Out','Unknown')");
            t.HasCheckConstraint("CK_AttendanceEvent_Source", "[Source] IN ('ManualAuthorized','Device','Imported')");
            t.HasCheckConstraint("CK_AttendanceEvent_TimeZone", "[BusinessTimeZone] = 'Asia/Bangkok'");
            t.HasCheckConstraint("CK_AttendanceEvent_EmploymentReadiness", "[EmploymentReadiness] IN ('Ready','NotEmployed','ConfigurationConflict')");
            t.HasCheckConstraint("CK_AttendanceEvent_SourceFields", "([Source] = 'ManualAuthorized' AND [ManualRequestKey] IS NOT NULL AND [ManualRequestKey] <> '00000000-0000-0000-0000-000000000000' AND [Reason] IS NOT NULL AND LEN(LTRIM(RTRIM([Reason]))) > 0 AND [SourceKey] IS NULL AND [ExternalEventId] IS NULL) OR ([Source] IN ('Device','Imported') AND [SourceKey] IS NOT NULL AND LEN(LTRIM(RTRIM([SourceKey]))) > 0 AND [ExternalEventId] IS NOT NULL AND LEN(LTRIM(RTRIM([ExternalEventId]))) > 0 AND [ManualRequestKey] IS NULL)");
        });
        b.HasKey(x => x.AttendanceEventId);
        b.Property(x => x.OccurredAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ReceivedAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.BusinessDate).HasColumnType("date");
        b.Property(x => x.BusinessTimeZone).HasMaxLength(50).IsRequired().UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Direction).HasMaxLength(20).IsRequired().UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.Source).HasMaxLength(30).IsRequired().UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.SourceKey).HasMaxLength(100).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.ExternalEventId).HasMaxLength(200).UseCollation("Latin1_General_100_BIN2");
        b.Property(x => x.OriginalSourceTimestamp).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.Property(x => x.EmploymentReadiness).HasMaxLength(30).IsRequired().UseCollation("Latin1_General_100_BIN2");
        b.HasIndex(x => x.ManualRequestKey).IsUnique().HasFilter("[ManualRequestKey] IS NOT NULL");
        b.HasIndex(x => new { x.SourceKey, x.ExternalEventId }).IsUnique().HasFilter("[ExternalEventId] IS NOT NULL");
        b.HasIndex(x => new { x.EmployeeId, x.BusinessDate, x.OccurredAtUtc, x.AttendanceEventId });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}
