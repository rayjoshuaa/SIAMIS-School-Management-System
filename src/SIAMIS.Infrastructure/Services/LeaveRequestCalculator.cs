using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Infrastructure.Services;

public sealed record LeaveCalculationContext(Guid EmployeeId, LeaveType LeaveType, IReadOnlyList<EmploymentRecord> Employment,
    IReadOnlyList<EmployeeWorkCalendarAssignment> Assignments, IReadOnlyList<WorkCalendar> Calendars,
    IReadOnlyList<WorkCalendarWeeklyInterval> Weekly, IReadOnlyList<WorkCalendarDateOverride> Overrides,
    IReadOnlyList<WorkCalendarOverrideInterval> OverrideIntervals, IReadOnlyList<LeavePolicy> Policies);

/// <summary>Pure scheduled-minute calculation. No balance writes, current-state approval recalculation, or payroll effects.</summary>
public static class LeaveRequestCalculator
{
    public const string BusinessTimeZone = "Asia/Bangkok";
    public static string? Shape(EmployeeLeaveRequest r)
    {
        if (!r.LeaveTypeId.HasValue || !r.StartDate.HasValue || !r.EndDate.HasValue || r.EndDate < r.StartDate) return "Leave type and valid inclusive date range are required.";
        if (!r.RequestMode.HasValue || !Enum.IsDefined(r.RequestMode.Value) || !r.NoticeCategory.HasValue || !Enum.IsDefined(r.NoticeCategory.Value)) return "Explicit FullDay/Timed mode and Foreseeable/SuddenIllness notice category are required.";
        if (r.RequestMode == LeaveRequestMode.FullDay && (r.RequestedStartTime.HasValue || r.RequestedEndTime.HasValue)) return "FullDay requests must not supply boundary times.";
        if (r.RequestMode == LeaveRequestMode.Timed && (!r.RequestedStartTime.HasValue || !r.RequestedEndTime.HasValue
            || r.RequestedStartTime.Value.Ticks % TimeSpan.TicksPerMinute != 0 || r.RequestedEndTime.Value.Ticks % TimeSpan.TicksPerMinute != 0
            || (r.StartDate == r.EndDate && r.RequestedStartTime >= r.RequestedEndTime))) return "Timed requests require whole-minute boundaries and a positive time range.";
        if (r.Reason?.Length > 1000) return "Reason cannot exceed 1000 characters.";
        if (r.NoticeCategory == LeaveNoticeCategory.SuddenIllness && string.IsNullOrWhiteSpace(r.Reason)) return "SuddenIllness requires an explanation.";
        return null;
    }
    private static ServiceResult<LeaveCalculationSnapshot> Conflict(string message) => ServiceResult<LeaveCalculationSnapshot>.Fail("conflict", message);
    public static ServiceResult<LeaveCalculationSnapshot> Calculate(LeaveCalculationContext c, EmployeeLeaveRequest r, DateTime requestedAtUtc)
    {
        var invalid = Shape(r); if (invalid is not null) return ServiceResult<LeaveCalculationSnapshot>.Fail("validation", invalid);
        var dates = new List<LeaveDateCalculation>();
        for (int day = r.StartDate!.Value.DayNumber; day <= r.EndDate!.Value.DayNumber; day++)
        {
            var date = DateOnly.FromDayNumber(day);
            var employment = c.Employment.Where(x => EmploymentIntegrity.Start(x) <= date && (!x.EndDate.HasValue || x.EndDate >= date)).ToArray();
            if (employment.Length != 1) return Conflict(employment.Length == 0 ? "Employment coverage is missing for a requested date; leave cannot cross employment gaps." : "Employment coverage is ambiguous.");
            if (EmploymentIntegrity.Dates(employment[0]) is not null) return Conflict("Employment history is inconsistent.");
            var assignments = c.Assignments.Where(x => x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).ToArray();
            if (assignments.Length != 1) return Conflict(assignments.Length == 0 ? "Work calendar not configured." : "Work calendar assignment is ambiguous.");
            var assignment = assignments[0]; var calendar = c.Calendars.SingleOrDefault(x => x.Id == assignment.WorkCalendarId);
            if (calendar is null) return Conflict("Assigned work calendar is missing.");
            var policies = c.Policies.Where(x => x.Status == "Published" && x.LeaveTypeId == c.LeaveType.Id && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).ToArray();
            if (policies.Length != 1) return Conflict(policies.Length == 0 ? "Published leave policy not configured." : "Published leave policy is ambiguous.");
            var policy = policies[0];
            var overrides = c.Overrides.Where(x => x.WorkCalendarId == calendar.Id && x.Date == date).ToArray();
            if (overrides.Length > 1) return Conflict("Calendar date override is ambiguous.");
            var o = overrides.SingleOrDefault();
            var schedule = o is not null
                ? c.OverrideIntervals.Where(x => x.WorkCalendarDateOverrideId == o.Id).Select(x => new ScheduledLeaveInterval(x.Id, x.StartTime, x.EndTime)).OrderBy(x => x.StartTime).ToArray()
                : c.Weekly.Where(x => x.WorkCalendarId == calendar.Id && x.DayOfWeek == date.DayOfWeek).Select(x => new ScheduledLeaveInterval(x.Id, x.StartTime, x.EndTime)).OrderBy(x => x.StartTime).ToArray();
            if (o is not null && (o.OverrideType is not ("PublicHoliday" or "SchoolHoliday" or "RestDay" or "ExceptionalWorkingDay") || (o.OverrideType == "ExceptionalWorkingDay" ? schedule.Length == 0 : schedule.Length != 0))) return Conflict("Calendar override configuration is inconsistent.");
            if (schedule.Any(x => x.StartTime >= x.EndTime || !WholeMinute(x.StartTime) || !WholeMinute(x.EndTime)) || schedule.Skip(1).Where((x, i) => x.StartTime < schedule[i].EndTime).Any()) return Conflict("Calendar working intervals are inconsistent.");
            var start = r.RequestMode == LeaveRequestMode.Timed && date == r.StartDate ? r.RequestedStartTime : null;
            var end = r.RequestMode == LeaveRequestMode.Timed && date == r.EndDate ? r.RequestedEndTime : null;
            var charged = schedule.Select(x => new WorkIntervalDto(start.HasValue && start > x.StartTime ? start.Value : x.StartTime,
                    end.HasValue && end < x.EndTime ? end.Value : x.EndTime)).Where(x => x.StartTime < x.EndTime).ToArray();
            dates.Add(new(date, employment[0].EmploymentRecordId, assignment.Id, calendar.Id, calendar.Code, calendar.Name,
                o?.OverrideType ?? "Weekly", o?.Id, Evidence(policy), schedule, charged, charged.Sum(Minutes)));
        }
        if (dates.Select(x => x.Policy.BalanceTracked).Distinct().Count() != 1) return Conflict("The request crosses incompatible leave-balance policies. Submit separate requests at the policy boundary.");
        var total = dates.Sum(x => (long)x.ChargeableMinutes);
        if (total == 0) return ServiceResult<LeaveCalculationSnapshot>.Fail("validation", "The request contains no chargeable scheduled working minutes.");
        if (total > int.MaxValue) return ServiceResult<LeaveCalculationSnapshot>.Fail("validation", "Chargeable minutes exceed supported storage precision.");
        var firstDate = dates.First(x => x.ChargeableMinutes > 0); var local = firstDate.Date.ToDateTime(firstDate.ChargedIntervals[0].StartTime, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZone));
        var hours = (utc - requestedAtUtc).TotalHours; var notice = dates.Select(x => x.Policy.ForeseeableNoticeHours).Max();
        var sudden = r.NoticeCategory == LeaveNoticeCategory.SuddenIllness;
        if (sudden && dates.Any(x => !x.Policy.AllowsSuddenRequest)) return ServiceResult<LeaveCalculationSnapshot>.Fail("validation", "SuddenIllness is not allowed by every applicable policy revision.");
        if (!sudden && notice.HasValue && hours < notice.Value) return ServiceResult<LeaveCalculationSnapshot>.Fail("validation", "The request does not meet the required foreseeable notice period.");
        int sequence = 0, longest = 0;
        foreach (var date in dates)
        {
            if (date.ChargeableMinutes > 0) { sequence++; longest = Math.Max(longest, sequence); }
            else if (date.ScheduledIntervals.Count > 0) sequence = 0;
        }
        var reasons = new HashSet<string>();
        foreach (var date in dates.Where(x => x.ChargeableMinutes > 0))
        {
            var p = date.Policy;
            if (p.SupportingDocumentPolicy == "AlwaysRequired") reasons.Add("AlwaysRequired");
            if (p.SupportingDocumentPolicy != "Conditional") continue;
            if (p.CertificateAfterConsecutiveDays.HasValue && longest > p.CertificateAfterConsecutiveDays.Value) reasons.Add("ConsecutiveDaysThreshold");
            if (p.CertificateOnMondayWorkingDate && date.Date.DayOfWeek == DayOfWeek.Monday) reasons.Add("MondayWorkingDate");
            if (p.CertificateOnFridayWorkingDate && date.Date.DayOfWeek == DayOfWeek.Friday) reasons.Add("FridayWorkingDate");
        }
        var allocations = dates.Where(x => x.ChargeableMinutes > 0).GroupBy(x => x.Date.Year).OrderBy(x => x.Key).Select(x => new LeaveAllocationDto(x.Key, x.Sum(y => y.ChargeableMinutes))).ToArray();
        var snapshot = new LeaveCalculationSnapshot(1, c.EmployeeId, c.LeaveType.Id, c.LeaveType.Code, c.LeaveType.Name, c.LeaveType.IsPaid,
            r.RequestMode!.Value.ToString(), r.StartDate.Value, r.EndDate.Value, r.RequestedStartTime, r.RequestedEndTime,
            r.NoticeCategory!.Value.ToString(), requestedAtUtc, BusinessTimeZone, local, utc, notice, hours, true, sudden,
            dates[0].Policy.BalanceTracked, dates, (int)total, allocations, longest, reasons.Count > 0, reasons.OrderBy(x => x).ToArray());
        var types = LeaveEvidenceRules.RequiredTypes(snapshot);
        if (!types.IsSuccess) return Conflict(types.Failure!.Message);
        return ServiceResult<LeaveCalculationSnapshot>.Success(snapshot with { RequiredDocumentTypeIds = types.Value });
    }
    public static bool WholeMinute(TimeOnly value) => value.Ticks % TimeSpan.TicksPerMinute == 0;
    public static int Minutes(WorkIntervalDto x) => (int)(x.EndTime.ToTimeSpan() - x.StartTime.ToTimeSpan()).TotalMinutes;
    internal static LeavePolicyEvidence Evidence(LeavePolicy p) => new(p.Id, p.Version, p.BalanceTracked, p.ForeseeableNoticeHours, p.AllowsSuddenRequest,
        p.SupportingDocumentPolicy, p.DocumentTypeId, p.CertificateAfterConsecutiveDays, p.CertificateOnMondayWorkingDate, p.CertificateOnFridayWorkingDate, p.SandwichParticipation, p.SandwichEquivalentDayMinutes);
    public static bool Overlap(LeaveCalculationSnapshot a, LeaveCalculationSnapshot b)
        => a.Dates.Any(x => b.Dates.Any(y => y.Date == x.Date && x.ChargedIntervals.Any(i => y.ChargedIntervals.Any(j => i.StartTime < j.EndTime && j.StartTime < i.EndTime))));
}
