using System.Globalization;
using System.Text.RegularExpressions;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Pure expected-work resolution. No pairing, leave calculation, daily result or monetary behavior.</summary>
public static class AttendanceFoundationResolver
{
    public const string BusinessTimeZone = LeaveRequestCalculator.BusinessTimeZone;
    public static bool TryInstant(string? text, out DateTime utc)
    {
        utc = default;
        if (text is null || !Regex.IsMatch(text, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant)) return false;
        utc = instant.UtcDateTime;
        // Bangkok conversion must remain inside SQL/.NET's supported date range.
        return utc <= DateTime.MaxValue.AddHours(-7);
    }
    public static DateOnly BusinessDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZone)));
    public static string EmploymentReadiness(IReadOnlyList<EmploymentRecord> effective)
        => effective.Count == 0 ? "NotEmployed" : effective.Count != 1 || EmploymentIntegrity.Dates(effective[0]) is not null ? "ConfigurationConflict" : "Ready";

    public static AttendanceExpectedWorkDto Resolve(Guid employeeId, bool isActive, DateOnly date,
        IReadOnlyList<EmploymentRecord> employment, IReadOnlyList<EmployeeWorkCalendarAssignment> assignments,
        IReadOnlyList<WorkCalendar> calendars, IReadOnlyList<WorkCalendarWeeklyInterval> weekly,
        IReadOnlyList<WorkCalendarDateOverride> overrides, IReadOnlyList<WorkCalendarOverrideInterval> replacement)
    {
        var result = new AttendanceExpectedWorkDto(employeeId, date, BusinessTimeZone, isActive, "Ready", null, null,
            null, null, null, null, null, null, []);
        var effective = employment.Where(x => EmploymentIntegrity.Start(x) <= date && (!x.EndDate.HasValue || x.EndDate >= date)).ToArray();
        var readiness = EmploymentReadiness(effective);
        if (readiness != "Ready") return result with { Readiness = readiness, Finding = readiness == "NotEmployed" ? "No effective employment on this date." : "Employment coverage is ambiguous or invalid." };
        var e = effective[0];
        result = result with { Employment = new(e.EmploymentRecordId, EmploymentIntegrity.Start(e), e.EndDate, e.DepartmentId, e.DesignationId, e.LocationId, e.EmploymentTypeId, e.EmploymentStatusId) };
        var matches = assignments.Where(x => x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).ToArray();
        if (matches.Length != 1) return result with { Readiness = matches.Length == 0 ? "WorkCalendarNotConfigured" : "ConfigurationConflict", Finding = matches.Length == 0 ? "An explicit effective work calendar assignment is required." : "Multiple calendar assignments cover this date." };
        var a = matches[0];
        result = result with { AssignmentId = a.Id, WorkCalendarId = a.WorkCalendarId };
        var calendar = calendars.SingleOrDefault(x => x.Id == a.WorkCalendarId);
        if (calendar is null || a.EffectiveTo < a.EffectiveFrom) return result with { Readiness = "ConfigurationConflict", Finding = "Assigned calendar or assignment dates are inconsistent." };
        result = result with { CalendarCode = calendar.Code, CalendarName = calendar.Name };
        var dated = overrides.Where(x => x.WorkCalendarId == calendar.Id && x.Date == date).ToArray();
        if (dated.Length > 1) return result with { Readiness = "ConfigurationConflict", Finding = "Multiple date overrides exist." };
        var o = dated.SingleOrDefault();
        var intervals = o is null
            ? weekly.Where(x => x.WorkCalendarId == calendar.Id && x.DayOfWeek == date.DayOfWeek).Select(x => new AttendanceScheduleInterval(x.Id, x.StartTime, x.EndTime)).OrderBy(x => x.StartTime).ToArray()
            : replacement.Where(x => x.WorkCalendarDateOverrideId == o.Id).Select(x => new AttendanceScheduleInterval(x.Id, x.StartTime, x.EndTime)).OrderBy(x => x.StartTime).ToArray();
        if (o is not null && (o.OverrideType is not ("PublicHoliday" or "SchoolHoliday" or "RestDay" or "ExceptionalWorkingDay")
            || (o.OverrideType == "ExceptionalWorkingDay" ? intervals.Length == 0 : intervals.Length != 0))
            || intervals.Any(x => x.StartTime >= x.EndTime || !LeaveRequestCalculator.WholeMinute(x.StartTime) || !LeaveRequestCalculator.WholeMinute(x.EndTime))
            || intervals.Skip(1).Where((x, i) => x.StartTime < intervals[i].EndTime).Any())
            return result with { Readiness = "ConfigurationConflict", Finding = "Working intervals or override configuration are inconsistent." };
        return result with { OverrideId = o?.Id, ScheduleKind = o?.OverrideType ?? (intervals.Length == 0 ? "WeeklyNonWorking" : "Weekly"), Intervals = intervals };
    }
}
