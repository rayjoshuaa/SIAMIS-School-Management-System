using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Services;

internal static class D9BFoundationTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var entity = model.FindEntityType(typeof(AttendanceEvent))!;
        check(entity.GetTableName() == "AttendanceEvents", "D9B focused event table");
        foreach (var name in new[] { "OccurredAtUtc", "ReceivedAtUtc" })
            check(entity.FindProperty(name)!.GetColumnType() == "datetime2(7)", "D9B precise UTC " + name);
        check(entity.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.NoAction, "D9B NoAction employee ownership");
        check(entity.GetIndexes().Count(x => x.IsUnique && x.GetFilter() is not null) == 2, "D9B filtered manual/external identity");
        check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "SourceKey", "ExternalEventId" })), "D9B external identity excludes employee");
        check(entity.GetCheckConstraints().Count() == 5, "D9B controlled source/direction/readiness checks");
        foreach (var forbidden in new[] { "Source", "ActorId", "EmployeeId", "ReceivedAtUtc", "BusinessDate", "SourceKey", "ExternalEventId" })
            check(typeof(ManualAttendanceEventRequest).GetProperty(forbidden) is null, "D9B server-controlled " + forbidden);
        foreach (var text in new[] { "2026-10-03T17:00:00.1234567Z", "2026-10-04T00:00:00.1234567+07:00" })
        {
            check(AttendanceFoundationResolver.TryInstant(text, out var utc), "D9B explicit instant " + text);
            check(utc.Ticks % TimeSpan.TicksPerSecond == 1234567, "D9B seven-digit fraction retained");
            check(AttendanceFoundationResolver.BusinessDate(utc) == new DateOnly(2026, 10, 4), "D9B Bangkok date");
        }
        foreach (var text in new[] { "2026-10-04T00:00:00", "2026-10-04T00:00:00.12345678Z", "invalid", "2026-02-30T00:00:00Z" })
            check(!AttendanceFoundationResolver.TryInstant(text, out _), "D9B ambiguous/invalid timestamp rejected");
        var employee = Guid.NewGuid(); var date = new DateOnly(2026, 10, 5);
        var employment = new EmploymentRecord { EmployeeId = employee, HireDate = date.AddDays(-1) };
        var calendar = new WorkCalendar { IsDefault = true, IsActive = false, Code = "HIST", Name = "Historical" };
        var assignment = new EmployeeWorkCalendarAssignment { EmployeeId = employee, WorkCalendarId = calendar.Id, EffectiveFrom = date, EffectiveTo = date };
        WorkCalendarWeeklyInterval Interval(TimeOnly start, TimeOnly end) => new() { WorkCalendarId = calendar.Id, DayOfWeek = date.DayOfWeek, StartTime = start, EndTime = end };
        var weekly = new[] { Interval(new(8, 0), new(12, 0)), Interval(new(13, 0), new(16, 0)) };
        AttendanceExpectedWorkDto Resolve(EmploymentRecord[] es, EmployeeWorkCalendarAssignment[] aa, WorkCalendarWeeklyInterval[] ww, WorkCalendarDateOverride[] oo, WorkCalendarOverrideInterval[] rr)
            => AttendanceFoundationResolver.Resolve(employee, false, date, es, aa, [calendar], ww, oo, rr);
        check(Resolve([], [assignment], weekly, [], []).Readiness == "NotEmployed", "D9B not employed creates no schedule");
        check(Resolve([employment], [], weekly, [], []).Readiness == "WorkCalendarNotConfigured", "D9B default never falls back");
        check(Resolve([employment, employment], [assignment], weekly, [], []).Readiness == "ConfigurationConflict", "D9B ambiguous employment");
        check(Resolve([employment], [assignment, assignment], weekly, [], []).Readiness == "ConfigurationConflict", "D9B ambiguous assignments");
        var ready = Resolve([employment], [assignment], weekly.Reverse().ToArray(), [], []);
        check(ready.Readiness == "Ready" && !ready.EmployeeIsActive && ready.Intervals.Count == 2, "D9B inactive employee/calendar historical resolution");
        check(ready.Intervals[0].StartTime == new TimeOnly(8, 0) && ready.Intervals[1].StartTime == new TimeOnly(13, 0), "D9B ordered split intervals with unpaid gap");
        check(Resolve([employment], [assignment], [], [], []).ScheduleKind == "WeeklyNonWorking", "D9B weekly nonworking");
        foreach (var kind in new[] { "PublicHoliday", "SchoolHoliday", "RestDay", "ExceptionalWorkingDay" })
        {
            var o = new WorkCalendarDateOverride { WorkCalendarId = calendar.Id, Date = date, OverrideType = kind };
            var replacement = new WorkCalendarOverrideInterval { WorkCalendarDateOverrideId = o.Id, StartTime = new(10, 0), EndTime = new(11, 0) };
            var r = Resolve([employment], [assignment], weekly, [o], kind == "ExceptionalWorkingDay" ? [replacement] : []);
            check(r.Readiness == "Ready" && r.ScheduleKind == kind && r.Intervals.Count == (kind == "ExceptionalWorkingDay" ? 1 : 0), "D9B override " + kind);
            check(Resolve([employment], [assignment], weekly, [o], kind == "ExceptionalWorkingDay" ? [] : [replacement]).Readiness == "ConfigurationConflict", "D9B invalid override " + kind);
        }
        check(Resolve([employment], [assignment], [weekly[0], Interval(new(11, 0), new(14, 0))], [], []).Readiness == "ConfigurationConflict", "D9B overlapping weekly intervals");
        using var db = new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=localhost;Database=Unused;Integrated Security=True;TrustServerCertificate=True").Options);
        foreach (var state in new[] { EntityState.Modified, EntityState.Deleted })
        {
            db.ChangeTracker.Clear(); db.Entry(new AttendanceEvent()).State = state;
            try { db.SaveChanges(); check(false, "D9B immutable guard"); }
            catch (InvalidOperationException e) { check(e.Message.Contains("immutable"), "D9B rejects EF " + state + " before SQL"); }
        }
    }
}
