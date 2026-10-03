using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Infrastructure.Configurations;

internal static class LeaveFoundationConfiguration
{
    private static EntityTypeBuilder<T> Table<T>(ModelBuilder m, string name) where T : LeaveFoundationRecord
    {
        var b = m.Entity<T>();
        b.HasBaseType((Type?)null);
        b.ToTable(name); b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        return b;
    }
    private const string Times = "[StartTime] < [EndTime] AND DATEPART(SECOND,[StartTime]) = 0 AND DATEPART(SECOND,[EndTime]) = 0";
    public static void Configure(ModelBuilder m)
    {
        var c = Table<WorkCalendar>(m, "WorkCalendars");
        c.Property(x => x.Code).HasMaxLength(50).IsRequired(); c.Property(x => x.Name).HasMaxLength(150).IsRequired();
        c.Property(x => x.Description).HasMaxLength(1000);
        c.HasIndex(x => x.Code).IsUnique();
        c.HasIndex(x => x.IsDefault).IsUnique().HasFilter("[IsDefault] = 1 AND [IsActive] = 1");
        var w = Table<WorkCalendarWeeklyInterval>(m, "WorkCalendarWeeklyIntervals");
        w.ToTable("WorkCalendarWeeklyIntervals", t => { t.HasCheckConstraint("CK_WeeklyInterval_Time", Times); t.HasCheckConstraint("CK_WeeklyInterval_Day", "[DayOfWeek] BETWEEN 0 AND 6"); });
        w.Property(x => x.StartTime).HasColumnType("time(0)"); w.Property(x => x.EndTime).HasColumnType("time(0)");
        w.HasIndex(x => new { x.WorkCalendarId, x.DayOfWeek, x.StartTime }).IsUnique();
        w.HasOne<WorkCalendar>().WithMany().HasForeignKey(x => x.WorkCalendarId).OnDelete(DeleteBehavior.NoAction);
        var o = Table<WorkCalendarDateOverride>(m, "WorkCalendarDateOverrides");
        o.ToTable("WorkCalendarDateOverrides", t => t.HasCheckConstraint("CK_DateOverride_Type", "[OverrideType] IN ('PublicHoliday','SchoolHoliday','RestDay','ExceptionalWorkingDay')"));
        o.Property(x => x.Date).HasColumnType("date"); o.Property(x => x.OverrideType).HasMaxLength(30);
        o.Property(x => x.Name).HasMaxLength(150); o.Property(x => x.Description).HasMaxLength(1000);
        o.HasIndex(x => new { x.WorkCalendarId, x.Date }).IsUnique();
        o.HasOne<WorkCalendar>().WithMany().HasForeignKey(x => x.WorkCalendarId).OnDelete(DeleteBehavior.NoAction);
        var i = Table<WorkCalendarOverrideInterval>(m, "WorkCalendarOverrideIntervals");
        i.ToTable("WorkCalendarOverrideIntervals", t => t.HasCheckConstraint("CK_OverrideInterval_Time", Times));
        i.Property(x => x.StartTime).HasColumnType("time(0)"); i.Property(x => x.EndTime).HasColumnType("time(0)");
        i.HasIndex(x => new { x.WorkCalendarDateOverrideId, x.StartTime }).IsUnique();
        i.HasOne<WorkCalendarDateOverride>().WithMany().HasForeignKey(x => x.WorkCalendarDateOverrideId).OnDelete(DeleteBehavior.NoAction);
        var a = Table<EmployeeWorkCalendarAssignment>(m, "EmployeeWorkCalendarAssignments");
        a.ToTable("EmployeeWorkCalendarAssignments", t => t.HasCheckConstraint("CK_CalendarAssignment_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"));
        a.Property(x => x.EffectiveFrom).HasColumnType("date"); a.Property(x => x.EffectiveTo).HasColumnType("date");
        a.HasIndex(x => new { x.EmployeeId, x.EffectiveFrom });
        a.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        a.HasOne<WorkCalendar>().WithMany().HasForeignKey(x => x.WorkCalendarId).OnDelete(DeleteBehavior.NoAction);
        var p = Table<LeavePolicy>(m, "LeavePolicies");
        p.Property(x => x.Version).HasMaxLength(50); p.Property(x => x.Status).HasMaxLength(20);
        p.Property(x => x.SupportingDocumentPolicy).HasMaxLength(30); p.Property(x => x.PublishedAt).HasColumnType("datetime2");
        p.Property(x => x.EffectiveFrom).HasColumnType("date"); p.Property(x => x.EffectiveTo).HasColumnType("date");
        p.HasIndex(x => new { x.LeaveTypeId, x.Version }).IsUnique();
        p.HasIndex(x => new { x.LeaveTypeId, x.Status, x.EffectiveFrom });
        p.ToTable("LeavePolicies", t => {
            t.HasCheckConstraint("CK_LeavePolicy_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            t.HasCheckConstraint("CK_LeavePolicy_Status", "([Status] = 'Draft' AND [PublishedAt] IS NULL) OR ([Status] = 'Published' AND [PublishedAt] IS NOT NULL)");
            t.HasCheckConstraint("CK_LeavePolicy_Notice", "[ForeseeableNoticeHours] IS NULL OR [ForeseeableNoticeHours] >= 0");
            t.HasCheckConstraint("CK_LeavePolicy_Document", "([SupportingDocumentPolicy] = 'None' AND [DocumentTypeId] IS NULL AND [CertificateAfterConsecutiveDays] IS NULL AND [CertificateOnMondayWorkingDate] = 0 AND [CertificateOnFridayWorkingDate] = 0) OR ([SupportingDocumentPolicy] IN ('AlwaysRequired','Conditional') AND [DocumentTypeId] IS NOT NULL)");
            t.HasCheckConstraint("CK_LeavePolicy_SandwichMinutes", "([SandwichEquivalentDayMinutes] IS NULL OR [SandwichEquivalentDayMinutes] > 0) AND ([SandwichParticipation] = 1 OR [SandwichEquivalentDayMinutes] IS NULL)");
            t.HasCheckConstraint("CK_LeavePolicy_Threshold", "[CertificateAfterConsecutiveDays] IS NULL OR [CertificateAfterConsecutiveDays] >= 0");
        });
        p.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);
        p.HasOne<DocumentType>().WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.NoAction);
        var e = Table<EmployeeLeaveEntitlement>(m, "EmployeeLeaveEntitlements");
        e.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.LeaveYear }).IsUnique();
        e.ToTable("EmployeeLeaveEntitlements", t => { t.HasCheckConstraint("CK_Entitlement_Year", "[LeaveYear] BETWEEN 1 AND 9999"); t.HasCheckConstraint("CK_Entitlement_Minutes", "[EntitledMinutes] >= 0"); });
        e.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.NoAction);
        var j = m.Entity<EmployeeLeaveEntitlementAdjustment>(); j.ToTable("EmployeeLeaveEntitlementAdjustments", t => t.HasCheckConstraint("CK_EntitlementAdjustment_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0 AND [AdjustmentMinutes] <> 0"));
        j.HasKey(x => x.Id); j.Property(x => x.Reason).HasMaxLength(1000).IsRequired(); j.Property(x => x.CreatedAt).HasColumnType("datetime2");
        j.HasOne<EmployeeLeaveEntitlement>().WithMany().HasForeignKey(x => x.EmployeeLeaveEntitlementId).OnDelete(DeleteBehavior.NoAction);
    }
}
