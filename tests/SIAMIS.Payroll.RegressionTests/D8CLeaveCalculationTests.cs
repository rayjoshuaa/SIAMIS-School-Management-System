using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Services;

internal static class D8CLeaveCalculationTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var employee = Guid.NewGuid(); var type = new LeaveType { Code = "SYN", Name = "Synthetic", IsPaid = true };
        var from = new DateOnly(2030, 1, 7); // Monday. Synthetic dates and policy values only.
        var calendar = new WorkCalendar { Code = "SYN", Name = "Synthetic calendar" };
        var job = new EmploymentRecord { EmployeeId = employee, HireDate = from.AddDays(-30), IsCurrent = true };
        var assignment = new EmployeeWorkCalendarAssignment { EmployeeId = employee, WorkCalendarId = calendar.Id, EffectiveFrom = from.AddDays(-30) };
        var policy = new LeavePolicy { LeaveTypeId = type.Id, Version = "Synthetic-1", Status = "Published", EffectiveFrom = from.AddDays(-30), BalanceTracked = true, AllowsSuddenRequest = true };
        var weekly = Enumerable.Range(1, 5).SelectMany(day => new[] {
            new WorkCalendarWeeklyInterval { WorkCalendarId = calendar.Id, DayOfWeek = (DayOfWeek)day, StartTime = new(8,0), EndTime = new(12,0) },
            new WorkCalendarWeeklyInterval { WorkCalendarId = calendar.Id, DayOfWeek = (DayOfWeek)day, StartTime = new(13,0), EndTime = new(16,0) } }).ToArray();
        var context = new LeaveCalculationContext(employee, type, [job], [assignment], [calendar], weekly, [], [], [policy]);
        var now = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        EmployeeLeaveRequest Request(DateOnly? start = null, DateOnly? end = null, string? a = null, string? b = null) => new()
        {
            LeaveTypeId = type.Id, StartDate = start ?? from, EndDate = end ?? start ?? from,
            RequestMode = a is null ? LeaveRequestMode.FullDay : LeaveRequestMode.Timed, NoticeCategory = LeaveNoticeCategory.Foreseeable,
            RequestedStartTime = a is null ? null : TimeOnly.Parse(a), RequestedEndTime = b is null ? null : TimeOnly.Parse(b)
        };
        LeaveCalculationSnapshot Good(EmployeeLeaveRequest r, int minutes, string label, LeaveCalculationContext? c = null)
        {
            var result = LeaveRequestCalculator.Calculate(c ?? context, r, now);
            check(result.IsSuccess && result.Value!.ChargeableMinutes == minutes, "D8C " + label);
            var s = result.Value!;
            check(s.Allocations.Sum(x => x.ChargeableMinutes) == minutes && s.Dates.Sum(x => x.ChargeableMinutes) == minutes, "D8C allocation/date sum " + label);
            return s;
        }
        void Bad(EmployeeLeaveRequest r, string code, string label, LeaveCalculationContext? c = null)
        { var result = LeaveRequestCalculator.Calculate(c ?? context, r, now); check(!result.IsSuccess && result.Failure!.Code == code, "D8C " + label); }
        var full = Good(Request(), 420, "split full day, not eight hours");
        check(full.IsPaid == true, "D8C Paid classification frozen");
        type.IsPaid = false;
        var unpaid = Good(Request(), 420, "unpaid minutes identical");
        check(unpaid.IsPaid == false && full.IsPaid == true, "D8C changing live paid classification preserves prior snapshot");
        type.IsPaid = true;
        Good(Request(), 240, "single interval full day", context with { Weekly = weekly.Where(x => x.StartTime.Hour == 8).ToArray() });
        Good(Request(a: "09:00", b: "11:00"), 120, "hourly");
        Good(Request(a: "11:00", b: "14:00"), 120, "lunch excluded");
        Bad(Request(a: "12:00", b: "13:00"), "validation", "lunch-only rejected");
        Bad(Request(from.AddDays(6)), "validation", "rest-only rejected");
        Good(Request(end: from.AddDays(2)), 1260, "multi-day full");
        Good(Request(end: from.AddDays(2), a: "14:00", b: "11:00"), 720, "multi-day timed boundaries");
        foreach (var kind in new[] { "PublicHoliday", "SchoolHoliday", "RestDay" })
        {
            var o = new WorkCalendarDateOverride { WorkCalendarId = calendar.Id, Date = from, OverrideType = kind };
            Bad(Request(), "validation", kind + " zero", context with { Overrides = [o] });
            Good(Request(end: from.AddDays(1)), 420, kind + " excluded", context with { Overrides = [o] });
        }
        var exceptional = new WorkCalendarDateOverride { WorkCalendarId = calendar.Id, Date = from, OverrideType = "ExceptionalWorkingDay" };
        Good(Request(), 135, "exceptional replaces weekly", context with { Overrides = [exceptional], OverrideIntervals = [new() { WorkCalendarDateOverrideId = exceptional.Id, StartTime = new(10, 0), EndTime = new(12, 15) }] });
        Bad(Request(), "conflict", "missing assignment no fallback", context with { Assignments = [] });
        Bad(Request(), "conflict", "ambiguous assignment", context with { Assignments = [assignment, assignment] });
        var secondCalendar = new WorkCalendar { Code = "SECOND", Name = "Second" };
        var secondAssignment = new EmployeeWorkCalendarAssignment { EmployeeId = employee, WorkCalendarId = secondCalendar.Id, EffectiveFrom = from.AddDays(1) };
        var firstAssignment = new EmployeeWorkCalendarAssignment { EmployeeId = employee, WorkCalendarId = calendar.Id, EffectiveFrom = assignment.EffectiveFrom, EffectiveTo = from };
        var switched = Good(Request(end: from.AddDays(1), a: "14:00", b: "11:00"), 180, "calendar switch timed", context with
        { Assignments = [firstAssignment, secondAssignment], Calendars = [calendar, secondCalendar], Weekly = weekly.Concat([new() { WorkCalendarId = secondCalendar.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new(10, 0), EndTime = new(15, 0) }]).ToArray() });
        check(switched.Dates[0].WorkCalendarId != switched.Dates[1].WorkCalendarId, "D8C both calendar identities retained");
        Bad(Request(job.HireDate.AddDays(-1)), "conflict", "before hire");
        var ended = new EmploymentRecord { EmployeeId = employee, HireDate = job.HireDate, EndDate = from.AddDays(-1), IsCurrent = false };
        Bad(Request(), "conflict", "after termination", context with { Employment = [ended] });
        var earlier = new EmploymentRecord { EmployeeId = employee, HireDate = job.HireDate, EndDate = from, IsCurrent = false };
        var later = new EmploymentRecord { EmployeeId = employee, HireDate = from.AddDays(1), IsCurrent = true };
        var adjacent = Good(Request(end: from.AddDays(2)), 1260, "adjacent employment", context with { Employment = [earlier, later] });
        check(adjacent.Dates.Select(x => x.EmploymentRecordId).Distinct().Count() == 2, "D8C employment coverage IDs frozen");
        later.HireDate = from.AddDays(2);
        Bad(Request(end: from.AddDays(2)), "conflict", "employment gap", context with { Employment = [earlier, later] });
        Bad(Request(), "conflict", "ambiguous employment", context with { Employment = [job, job] });
        Good(Request(), 420, "future valid employment");
        var draft = new LeavePolicy { LeaveTypeId = type.Id, Version = "Draft", EffectiveFrom = policy.EffectiveFrom };
        Bad(Request(), "conflict", "draft ignored", context with { Policies = [draft] });
        Bad(Request(), "conflict", "missing policy", context with { Policies = [] });
        Bad(Request(), "conflict", "ambiguous policies", context with { Policies = [policy, policy] });
        var p1 = new LeavePolicy { LeaveTypeId = type.Id, Version = "P1", Status = "Published", EffectiveFrom = policy.EffectiveFrom, EffectiveTo = from, BalanceTracked = true, ForeseeableNoticeHours = 1, AllowsSuddenRequest = true };
        var p2 = new LeavePolicy { LeaveTypeId = type.Id, Version = "P2", Status = "Published", EffectiveFrom = from.AddDays(1), BalanceTracked = true, ForeseeableNoticeHours = 24, AllowsSuddenRequest = true };
        var revisions = Good(Request(end: from.AddDays(1)), 840, "same tracking revisions", context with { Policies = [p1, p2] });
        check(revisions.EffectiveNoticeHours == 24 && revisions.Dates.Select(x => x.Policy.Id).Distinct().Count() == 2, "D8C MAX notice and both policy revisions");
        p2.BalanceTracked = false;
        Bad(Request(end: from.AddDays(1)), "conflict", "mixed tracking rejected", context with { Policies = [p1, p2] }); p2.BalanceTracked = true;
        p2.ForeseeableNoticeHours = 999;
        Bad(Request(end: from.AddDays(1)), "validation", "MAX notice fails", context with { Policies = [p1, p2] }); p2.ForeseeableNoticeHours = null;
        var sudden = Request(end: from.AddDays(1)); sudden.NoticeCategory = LeaveNoticeCategory.SuddenIllness; sudden.Reason = "Synthetic sudden reason";
        Good(sudden, 840, "sudden bypass notice", context with { Policies = [p1, p2] });
        p2.AllowsSuddenRequest = false;
        Bad(sudden, "validation", "sudden every policy must allow", context with { Policies = [p1, p2] });
        sudden.Reason = " "; Bad(sudden, "validation", "sudden explanation required");
        check(full.BusinessTimeZone == "Asia/Bangkok" && full.FirstChargeableUtcStart.Hour == 1 && full.FirstChargeableLocalStart.Hour == 8, "D8C explicit Bangkok conversion");
        var midnightNow = new DateTime(2030, 1, 6, 18, 0, 0, DateTimeKind.Utc); // 01:00 Bangkok; first working interval eight local = seven hours notice.
        policy.ForeseeableNoticeHours = 7;
        check(LeaveRequestCalculator.Calculate(context, Request(), midnightNow).IsSuccess, "D8C inclusive exact notice boundary");
        policy.ForeseeableNoticeHours = 8;
        check(!LeaveRequestCalculator.Calculate(context, Request(), midnightNow).IsSuccess, "D8C first working interval notice failure"); policy.ForeseeableNoticeHours = null;
        policy.SupportingDocumentPolicy = "Conditional"; policy.CertificateAfterConsecutiveDays = 1; policy.CertificateOnMondayWorkingDate = true; policy.CertificateOnFridayWorkingDate = true; policy.SandwichParticipation = true;
        var cert = Good(Request(end: from.AddDays(1)), 840, "conditional certificate");
        check(cert.CertificateRequirementReasons.SequenceEqual(new[] { "ConsecutiveDaysThreshold", "MondayWorkingDate" }), "D8C certificate reasons and exclusive threshold");
        check(cert.Dates.All(x => x.Policy.SandwichParticipation), "D8C sandwich metadata only");
        var partial = Good(Request(a: "09:00", b: "09:01"), 1, "one minute is one qualifying working date");
        check(partial.LongestConsecutiveQualifyingDays == 1 && !partial.CertificateRequirementReasons.Contains("ConsecutiveDaysThreshold"), "D8C threshold equality does not trigger");
        var friday = Good(Request(from.AddDays(4)), 420, "Friday trigger");
        check(friday.CertificateRequirementReasons.Contains("FridayWorkingDate"), "D8C working Friday only");
        var weekend = Good(Request(from.AddDays(4), from.AddDays(7)), 840, "weekend zero does not break sequence");
        check(weekend.LongestConsecutiveQualifyingDays == 2 && weekend.Dates.Where(x => x.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).All(x => x.ChargeableMinutes == 0), "D8C certificate sequence does not charge sandwich");
        var zeroBoundary = Good(Request(end: from.AddDays(1), a: "17:00", b: "09:00"), 60, "working zero boundary breaks sequence");
        check(zeroBoundary.LongestConsecutiveQualifyingDays == 1 && !zeroBoundary.CertificateRequirementReasons.Contains("MondayWorkingDate"), "D8C uncharged Monday not qualifying");
        var holidayMonday = new WorkCalendarDateOverride { WorkCalendarId = calendar.Id, Date = from, OverrideType = "PublicHoliday" };
        var noMonday = Good(Request(end: from.AddDays(1)), 420, "nonworking Monday", context with { Overrides = [holidayMonday] });
        check(!noMonday.CertificateRequirementReasons.Contains("MondayWorkingDate"), "D8C holiday Monday no certificate trigger");
        policy.SupportingDocumentPolicy = "AlwaysRequired";
        check(Good(Request(), 420, "always document metadata").SupportingDocumentRequired, "D8C document metadata exposed without files");
        policy.SupportingDocumentPolicy = "None";
        var year = Good(Request(new(2030, 12, 31), new(2031, 1, 1)), 840, "cross year");
        check(year.Allocations.Count == 2 && year.Allocations.All(x => x.ChargeableMinutes == 420), "D8C separate calendar year rows");
        var a = Good(Request(a: "09:00", b: "11:00"), 120, "overlap first");
        var b = Good(Request(a: "11:00", b: "12:00"), 60, "adjacent second");
        check(!LeaveRequestCalculator.Overlap(a, b), "D8C touching intervals nonoverlap");
        check(LeaveRequestCalculator.Overlap(a, Good(Request(a: "10:00", b: "14:00"), 180, "overlap second")), "D8C real overlap");
        check(LeaveRequestCalculator.Overlap(full, a), "D8C full day overlaps timed");
        foreach (var r in new[] { new EmployeeLeaveRequest(), Request(a: "09:00:01", b: "11:00"), Request(a: "11:00", b: "10:00") }) Bad(r, "validation", "invalid shape");
        var bogus = Request(); bogus.RequestedStartTime = new(9, 0); Bad(bogus, "validation", "full day rejects guessed time");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        check(Enum.GetNames<LeaveCancellationExpectedStatus>().SequenceEqual(new[] { "Pending", "Approved" }), "D8C cancellation source state restricted");
        foreach (var status in new[] { "Pending", "Approved" })
            check(JsonSerializer.Deserialize<LeaveCancellationRequest>("{\"expectedStatus\":\"" + status + "\"}", options)!.ExpectedStatus!.Value.ToString() == status, "D8C cancellation accepts " + status);
        foreach (var value in new[] { "\"Rejected\"", "\"Cancelled\"", "\"Other\"", "\"\"", "0", "1", "9" })
        {
            bool rejected = false;
            try { JsonSerializer.Deserialize<LeaveCancellationRequest>("{\"expectedStatus\":" + value + "}", options); } catch (JsonException) { rejected = true; }
            check(rejected, "D8C cancellation rejects " + value);
        }
        foreach (var json in new[] { "{}", "{\"expectedStatus\":null}" })
        {
            var dto = JsonSerializer.Deserialize<LeaveCancellationRequest>(json, options)!;
            check(!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dto, new(dto), [], true), "D8C cancellation expected state required");
        }
        foreach (var json in new[] { "{\"status\":\"Cancelled\"}", "{\"reviewedAt\":null}" })
        {
            bool rejected = false; try { JsonSerializer.Deserialize<LeaveCancellationRequest>(json, options); } catch (JsonException) { rejected = true; }
            check(rejected, "D8C cancellation rejects forged mutation " + json);
        }
        foreach (var field in new[] { "status", "days", "chargeableMinutes", "calculationSnapshotJson", "requestedAt", "reviewedAt", "cancelledAt", "reviewerId", "availableMinutes" })
        {
            bool rejected = false; try { JsonSerializer.Deserialize<EmployeeLeaveRequest>("{\"" + field + "\":null}", options); } catch (JsonException) { rejected = true; }
            check(rejected, "D8C strict request rejects " + field);
        }
        var header = new EmployeeLeave { EmployeeId = employee, LeaveTypeId = type.Id, StartDate = full.StartDate, EndDate = full.EndDate, RequestMode = full.RequestMode,
            NoticeCategory = full.NoticeCategory, RequestedAt = full.RequestedAt, BalanceTracked = full.BalanceTracked, ChargeableMinutes = full.ChargeableMinutes,
            Days = 1, CalculationSnapshotVersion = 1, CalculationSnapshotJson = JsonSerializer.Serialize(full, options) };
        var allocations = full.Allocations.Select(x => new EmployeeLeaveAllocation { EmployeeLeaveId = header.LeaveId, LeaveYear = x.LeaveYear, ChargeableMinutes = x.ChargeableMinutes }).ToArray();
        check(LeaveSnapshotIntegrity.Read(header, allocations).IsSuccess, "D8C valid frozen evidence accepted after live policy changes");
        header.CalculationSnapshotJson = JsonSerializer.Serialize(full with { IsPaid = null }, options);
        check(!LeaveSnapshotIntegrity.Read(header, allocations).IsSuccess, "D8C missing historical paid classification requires integrity review");
        header.CalculationSnapshotJson = JsonSerializer.Serialize(full, options);
        allocations[0].ChargeableMinutes++; check(!LeaveSnapshotIntegrity.Read(header, allocations).IsSuccess, "D8C allocation mismatch rejected"); allocations[0].ChargeableMinutes--;
        header.CalculationSnapshotJson = "{}"; check(!LeaveSnapshotIntegrity.Read(header, allocations).IsSuccess, "D8C incomplete evidence rejected");
        header.CalculationSnapshotJson = JsonSerializer.Serialize(full with { FirstChargeableUtcStart = full.FirstChargeableUtcStart.AddHours(1) }, options);
        check(!LeaveSnapshotIntegrity.Read(header, allocations).IsSuccess, "D8C forged timezone evidence rejected");
        var entity = model.FindEntityType(typeof(EmployeeLeaveAllocation))!;
        check(entity.GetForeignKeys().Single().DeleteBehavior == DeleteBehavior.NoAction, "D8C allocation NoAction history");
        check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "EmployeeLeaveId", "LeaveYear" })), "D8C unique allocation per leave/year");
        foreach (var name in new[] { "RequestedAt", "ReviewedAt", "CancelledAt" }) check(model.FindEntityType(typeof(EmployeeLeave))!.FindProperty(name)!.GetColumnType() == "datetime2", "D8C UTC lifecycle storage " + name);
        check(typeof(EmployeeLeaveRequest).GetProperty("StorageKey") is null && typeof(LeaveCalculationSnapshot).GetProperty("AvailableMinutes") is null, "D8C no evidence storage or balance in snapshot");
    }
}
