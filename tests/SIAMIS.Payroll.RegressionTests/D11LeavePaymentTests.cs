using System.Text.Json;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Services;

internal static class D11LeavePaymentTests
{
    public static void Run(Action<bool, string> check)
    {
        var employee = Guid.NewGuid(); var type = new LeaveType { Name = "Synthetic D11", IsPaid = true };
        var first = new DateOnly(2030, 1, 7); var calendar = new WorkCalendar { Code = "D11", Name = "Synthetic" };
        var policy = new LeavePolicy { LeaveTypeId = type.Id, Version = "Synthetic", Status = "Published", EffectiveFrom = first.AddYears(-1), BalanceTracked = true, AllowsSuddenRequest = true };
        var employment = new EmploymentRecord { EmployeeId = employee, HireDate = first.AddYears(-1), IsCurrent = true };
        var assignment = new EmployeeWorkCalendarAssignment { EmployeeId = employee, WorkCalendarId = calendar.Id, EffectiveFrom = first.AddYears(-1) };
        var weekly = Enumerable.Range(1, 5).Select(day => new WorkCalendarWeeklyInterval { WorkCalendarId = calendar.Id, DayOfWeek = (DayOfWeek)day, StartTime = new(8, 0), EndTime = new(16, 0) }).ToArray();
        var context = new LeaveCalculationContext(employee, type, [employment], [assignment], [calendar], weekly, [], [], [policy]);
        LeaveCalculationSnapshot Calculate(DateOnly start, DateOnly end) => LeaveRequestCalculator.Calculate(context,
            new() { LeaveTypeId = type.Id, StartDate = start, EndDate = end, RequestMode = LeaveRequestMode.FullDay, NoticeCategory = LeaveNoticeCategory.Foreseeable }, first.AddDays(-1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).Value!;
        var source = Calculate(first, first.AddDays(3));
        foreach (var pair in new[] { (3000L, 1920), (1920L, 1920), (960L, 960), (0L, 0), (180L, 180), (-10L, 0) })
        {
            var v2 = LeavePaymentAllocation.Classify(source, new Dictionary<int, long> { [2030] = pair.Item1 });
            check(v2.Version == 2 && v2.Allocations.Single().PaidMinutes == pair.Item2, "D11 paid cap " + pair.Item1);
            check(v2.Allocations.Single().UnpaidMinutes == 1920 - pair.Item2, "D11 unpaid remainder " + pair.Item1);
            var header = new EmployeeLeave { EmployeeId = employee, LeaveTypeId = type.Id, StartDate = v2.StartDate, EndDate = v2.EndDate, RequestMode = v2.RequestMode, NoticeCategory = v2.NoticeCategory, RequestedAt = v2.RequestedAt, BalanceTracked = v2.BalanceTracked, ChargeableMinutes = v2.ChargeableMinutes, Days = 4, CalculationSnapshotVersion = 2, CalculationSnapshotJson = JsonSerializer.Serialize(v2, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
            var allocation = new EmployeeLeaveAllocation { EmployeeLeaveId = header.LeaveId, LeaveYear = 2030, ChargeableMinutes = 1920, PaidMinutes = pair.Item2, UnpaidMinutes = 1920 - pair.Item2 };
            check(LeaveSnapshotIntegrity.Read(header, [allocation]).IsSuccess, "D11 V2 integrity " + pair.Item1);
            allocation.PaidMinutes++; check(!LeaveSnapshotIntegrity.Read(header, [allocation]).IsSuccess, "D11 relational tamper rejected " + pair.Item1);
        }
        var partial = LeavePaymentAllocation.Classify(source, new Dictionary<int, long> { [2030] = 180 });
        check(partial.Dates[0].PaymentIntervals!.SequenceEqual(new[] { new ClassifiedLeaveInterval(new(8, 0), new(11, 0), true), new ClassifiedLeaveInterval(new(11, 0), new(16, 0), false) }), "D11 whole-minute partial-day split");
        check(partial.Dates.Skip(1).All(d => d.PaymentIntervals!.All(i => !i.IsPaid)), "D11 chronological exhaustion");
        var untracked = LeavePaymentAllocation.Classify(source with { BalanceTracked = false }, new Dictionary<int, long>());
        check(untracked.Allocations.Single().PaidMinutes == 1920, "D11 paid untracked fully paid without entitlement");
        foreach (var tracked in new[] { false, true })
            check(LeavePaymentAllocation.Classify(source with { IsPaid = false, BalanceTracked = tracked }, new Dictionary<int, long>()).Allocations.Single().PaidMinutes == 0, "D11 unpaid never consumes paid entitlement " + tracked);
        var cross = Calculate(new(2030, 12, 31), new(2031, 1, 2));
        var split = LeavePaymentAllocation.Classify(cross, new Dictionary<int, long> { [2030] = 120, [2031] = 600 });
        check(split.Allocations[0].PaidMinutes == 120 && split.Allocations[1].PaidMinutes == 600, "D11 independent calendar-year budgets");
        check(!JsonSerializer.Serialize(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Contains("paymentIntervals"), "D11 V1 frozen serialization has no fabricated classification");
        var rest = Calculate(first.AddDays(4), first.AddDays(7));
        check(LeavePaymentAllocation.Classify(rest, new Dictionary<int, long> { [2030] = 480 }).Dates.Where(d => d.ChargeableMinutes == 0).All(d => d.PaymentIntervals!.Count == 0), "D11 nonworking dates remain unclassified zero charge");
        check(source.Version == 1 && source.Dates.All(d => d.PaymentIntervals is null), "D11 classifier does not mutate V1 source");
        var date = source.Dates[0];
        var work = new AttendanceExpectedWorkDto(employee, first, "Asia/Bangkok", false, "Ready", null,
            new(employment.EmploymentRecordId, first.AddDays(-1), null, null, null, null, null, null), assignment.Id, calendar.Id,
            "D11", "Synthetic", null, "Weekly", date.ScheduledIntervals.Select(i => new AttendanceScheduleInterval(i.Id, i.StartTime, i.EndTime)).ToArray());
        AttendanceDayDto Day(int version, LeaveDateCalculation evidence) => AttendanceDayCalculator.Calculate(work, [],
            [new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Approved", version, true, evidence)], [], DateTime.UtcNow);
        var v1Day = Day(1, date);
        var originalFingerprintLeave = AttendanceReviewSources.Serialize(v1Day.ApprovedLeaves.OrderBy(x => x.LeaveId).Select(x => new {
            x.LeaveId, x.SnapshotVersion, x.IsPaid, x.Date.Date, x.Date.EmploymentRecordId, x.Date.AssignmentId, x.Date.WorkCalendarId,
            x.Date.OverrideId, x.Date.ScheduleSource, x.Date.ScheduledIntervals, x.Date.ChargedIntervals }));
        check(AttendanceReviewSources.Sources(v1Day, []).Leave == originalFingerprintLeave, "D11 V1 Attendance fingerprint bytes unchanged");
        var v2Day = Day(2, partial.Dates[0]);
        check(v2Day.PaidLeaveCoveredMilliseconds == 180 * 60000L && v2Day.UnpaidLeaveCoveredMilliseconds == 300 * 60000L
            && v2Day.UnexplainedScheduledMilliseconds == 0, "D11 mixed paid/unpaid Attendance authorized coverage");
        check(AttendanceReviewSources.Sources(v2Day, []).Leave.Contains("paymentIntervals"), "D11 V2 classification included in Attendance fingerprint");
        check(AttendanceReviewSources.Fingerprint(AttendanceReviewSources.Sources(v2Day, [])) != AttendanceReviewSources.Fingerprint(
            AttendanceReviewSources.Sources(Day(2, LeavePaymentAllocation.Classify(source, new Dictionary<int, long> { [2030] = 0 }).Dates[0]), [])),
            "D11 classification change detected as Attendance source change");

    }
}
