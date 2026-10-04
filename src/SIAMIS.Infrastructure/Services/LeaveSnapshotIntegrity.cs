using System.Text.Json;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Checks frozen evidence against its header and relational allocations, never against live configuration.</summary>
public static class LeaveSnapshotIntegrity
{
    public static ServiceResult<LeaveCalculationSnapshot> Read(EmployeeLeave leave, IReadOnlyList<EmployeeLeaveAllocation> allocations)
    {
        ServiceResult<LeaveCalculationSnapshot> Invalid() => ServiceResult<LeaveCalculationSnapshot>.Fail("conflict", "Stored leave calculation or allocation evidence is incomplete or inconsistent; integrity review is required.");
        try
        {
            if (leave.CalculationSnapshotVersion is not (1 or 2) || leave.CalculationSnapshotJson is null) return Invalid();
            var s = JsonSerializer.Deserialize<LeaveCalculationSnapshot>(leave.CalculationSnapshotJson, EmployeeLeaveService.SnapshotJson);
            if (s is null || s.Version != leave.CalculationSnapshotVersion || !s.IsPaid.HasValue || s.EmployeeId != leave.EmployeeId || s.LeaveTypeId != leave.LeaveTypeId
                || s.RequestMode != leave.RequestMode || s.NoticeCategory != leave.NoticeCategory || s.StartDate != leave.StartDate || s.EndDate != leave.EndDate
                || s.RequestedStartTime != leave.RequestedStartTime || s.RequestedEndTime != leave.RequestedEndTime || s.RequestedAt != leave.RequestedAt
                || s.BalanceTracked != leave.BalanceTracked || s.ChargeableMinutes != leave.ChargeableMinutes || s.ChargeableMinutes <= 0
                || s.BusinessTimeZone != LeaveRequestCalculator.BusinessTimeZone || !s.NoticeSatisfied
                || s.Dates.Count != s.EndDate.DayNumber - s.StartDate.DayNumber + 1) return Invalid();
            var shape = new EmployeeLeaveRequest { LeaveTypeId = s.LeaveTypeId, StartDate = s.StartDate, EndDate = s.EndDate,
                RequestMode = Enum.Parse<LeaveRequestMode>(s.RequestMode), NoticeCategory = Enum.Parse<LeaveNoticeCategory>(s.NoticeCategory),
                RequestedStartTime = s.RequestedStartTime, RequestedEndTime = s.RequestedEndTime, Reason = leave.Reason };
            if (LeaveRequestCalculator.Shape(shape) is not null) return Invalid();
            for (int i = 0; i < s.Dates.Count; i++)
            {
                var d = s.Dates[i];
                if (d.Date.DayNumber != s.StartDate.DayNumber + i || d.EmploymentRecordId == Guid.Empty || d.AssignmentId == Guid.Empty || d.WorkCalendarId == Guid.Empty
                    || d.Policy.Id == Guid.Empty || string.IsNullOrWhiteSpace(d.Policy.Version) || d.Policy.BalanceTracked != s.BalanceTracked
                    || d.ScheduleSource is not ("Weekly" or "ExceptionalWorkingDay" or "PublicHoliday" or "SchoolHoliday" or "RestDay")
                    || (d.ScheduleSource == "Weekly" ? d.OverrideId.HasValue : !d.OverrideId.HasValue)
                    || d.ScheduleSource is "PublicHoliday" or "SchoolHoliday" or "RestDay" && d.ScheduledIntervals.Count != 0
                    || d.ScheduleSource == "ExceptionalWorkingDay" && d.ScheduledIntervals.Count == 0) return Invalid();
                var schedule = d.ScheduledIntervals;
                if (schedule.Any(x => x.Id == Guid.Empty || x.StartTime >= x.EndTime || !LeaveRequestCalculator.WholeMinute(x.StartTime) || !LeaveRequestCalculator.WholeMinute(x.EndTime))
                    || schedule.Skip(1).Where((x, n) => x.StartTime < schedule[n].EndTime).Any()) return Invalid();
                var start = s.RequestMode == "Timed" && d.Date == s.StartDate ? s.RequestedStartTime : null;
                var end = s.RequestMode == "Timed" && d.Date == s.EndDate ? s.RequestedEndTime : null;
                var charged = schedule.Select(x => new WorkIntervalDto(start.HasValue && start > x.StartTime ? start.Value : x.StartTime,
                    end.HasValue && end < x.EndTime ? end.Value : x.EndTime)).Where(x => x.StartTime < x.EndTime).ToArray();
                if (!charged.SequenceEqual(d.ChargedIntervals) || charged.Sum(LeaveRequestCalculator.Minutes) != d.ChargeableMinutes) return Invalid();
            }
            var expected = s.Dates.Where(x => x.ChargeableMinutes > 0).GroupBy(x => x.Date.Year).OrderBy(x => x.Key)
                .Select(x => new LeaveAllocationDto(x.Key, x.Sum(y => y.ChargeableMinutes))).ToArray();
            if (s.Version == 1)
            {
                if (s.Dates.Any(d => d.PaymentIntervals is not null) || s.Allocations.Any(a => a.PaidMinutes.HasValue || a.UnpaidMinutes.HasValue)
                    || allocations.Any(a => a.PaidMinutes.HasValue || a.UnpaidMinutes.HasValue)) return Invalid();
            }
            else
            {
                if (s.Allocations.Any(a => !a.PaidMinutes.HasValue || !a.UnpaidMinutes.HasValue || a.PaidMinutes < 0 || a.UnpaidMinutes < 0
                    || (long)a.PaidMinutes + a.UnpaidMinutes != a.ChargeableMinutes)
                    || s.Dates.Any(d => d.PaymentIntervals is null)) return Invalid();
                var classified = LeavePaymentAllocation.Classify(s, s.Allocations.ToDictionary(a => a.LeaveYear, a => (long)a.PaidMinutes!.Value));
                if (!classified.Allocations.SequenceEqual(s.Allocations)
                    || classified.Dates.Where((d, n) => !d.PaymentIntervals!.SequenceEqual(s.Dates[n].PaymentIntervals!)).Any()) return Invalid();
                expected = expected.Select(a =>
                {
                    var paid = s.Dates.Where(d => d.Date.Year == a.LeaveYear).SelectMany(d => d.PaymentIntervals!)
                        .Where(i => i.IsPaid).Sum(i => LeaveRequestCalculator.Minutes(new(i.StartTime, i.EndTime)));
                    return a with { PaidMinutes = paid, UnpaidMinutes = a.ChargeableMinutes - paid };
                }).ToArray();
            }
            if (s.Dates.Sum(x => (long)x.ChargeableMinutes) != s.ChargeableMinutes || leave.Days != s.Dates.Count(x => x.ChargeableMinutes > 0)
                || !expected.SequenceEqual(s.Allocations) || allocations.Any(x => x.EmployeeLeaveId != leave.LeaveId)
                || !expected.SequenceEqual(allocations.OrderBy(x => x.LeaveYear).Select(x => new LeaveAllocationDto(x.LeaveYear, x.ChargeableMinutes) { PaidMinutes = x.PaidMinutes, UnpaidMinutes = x.UnpaidMinutes }))) return Invalid();
            var first = s.Dates.First(x => x.ChargeableMinutes > 0);
            var local = first.Date.ToDateTime(first.ChargedIntervals[0].StartTime, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.FindSystemTimeZoneById(s.BusinessTimeZone));
            var notice = s.Dates.Select(x => x.Policy.ForeseeableNoticeHours).Max();
            var sudden = s.NoticeCategory == "SuddenIllness";
            if (s.FirstChargeableLocalStart != local || s.FirstChargeableUtcStart != utc || s.EffectiveNoticeHours != notice || s.SuddenRequestUsed != sudden
                || s.ActualNoticeHours != (utc - s.RequestedAt).TotalHours || (sudden ? s.Dates.Any(x => !x.Policy.AllowsSuddenRequest) : notice.HasValue && s.ActualNoticeHours < notice.Value)) return Invalid();
            int sequence = 0, longest = 0;
            foreach (var d in s.Dates) { if (d.ChargeableMinutes > 0) { sequence++; longest = Math.Max(longest, sequence); } else if (d.ScheduledIntervals.Count > 0) sequence = 0; }
            var reasons = new HashSet<string>();
            foreach (var d in s.Dates.Where(x => x.ChargeableMinutes > 0))
            {
                var p = d.Policy;
                if (p.SupportingDocumentPolicy == "AlwaysRequired") reasons.Add("AlwaysRequired");
                if (p.SupportingDocumentPolicy != "Conditional") continue;
                if (p.CertificateAfterConsecutiveDays.HasValue && longest > p.CertificateAfterConsecutiveDays.Value) reasons.Add("ConsecutiveDaysThreshold");
                if (p.CertificateOnMondayWorkingDate && d.Date.DayOfWeek == DayOfWeek.Monday) reasons.Add("MondayWorkingDate");
                if (p.CertificateOnFridayWorkingDate && d.Date.DayOfWeek == DayOfWeek.Friday) reasons.Add("FridayWorkingDate");
            }
            if (s.LongestConsecutiveQualifyingDays != longest || s.SupportingDocumentRequired != (reasons.Count > 0) || !reasons.SetEquals(s.CertificateRequirementReasons)) return Invalid();
            var required = LeaveEvidenceRules.RequiredTypes(s);
            if (!required.IsSuccess || s.RequiredDocumentTypeIds is not null && !s.RequiredDocumentTypeIds.SequenceEqual(required.Value!)) return Invalid();
            return ServiceResult<LeaveCalculationSnapshot>.Success(s);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or NullReferenceException or OverflowException or KeyNotFoundException) { return Invalid(); }
    }
}
