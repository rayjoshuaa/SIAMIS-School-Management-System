using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Services;

internal static class D8DEvidenceContractTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var e = Guid.NewGuid(); var t = new LeaveType(); var calendar = new WorkCalendar(); var document = Guid.NewGuid();
        var monday = new DateOnly(2030, 1, 7);
        var policy = new LeavePolicy { LeaveTypeId = t.Id, Version = "Synthetic", Status = "Published", EffectiveFrom = monday,
            SupportingDocumentPolicy = "Conditional", DocumentTypeId = document, CertificateAfterConsecutiveDays = 1,
            CertificateOnMondayWorkingDate = true, SandwichParticipation = true, SandwichEquivalentDayMinutes = 135 };
        var context = new LeaveCalculationContext(e, t, [new EmploymentRecord { EmployeeId = e, HireDate = monday }],
            [new EmployeeWorkCalendarAssignment { EmployeeId = e, WorkCalendarId = calendar.Id, EffectiveFrom = monday }], [calendar],
            [new WorkCalendarWeeklyInterval { WorkCalendarId = calendar.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new(9,0), EndTime = new(11,15) }], [], [], [policy]);
        var request = new EmployeeLeaveRequest { LeaveTypeId = t.Id, StartDate = monday, EndDate = monday, RequestMode = LeaveRequestMode.FullDay, NoticeCategory = LeaveNoticeCategory.Foreseeable };
        var s = LeaveRequestCalculator.Calculate(context, request, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Value!;
        check(s.RequiredDocumentTypeIds!.SequenceEqual(new[] { document }), "D8D frozen requirement mapping");
        check(s.Dates[0].Policy.SandwichEquivalentDayMinutes == 135 && s.ChargeableMinutes == 135, "D8D explicit policy debit metadata, independent schedule");
        check(LeaveEvidenceRules.RequiredTypes(s with { RequiredDocumentTypeIds = null }).Value!.SequenceEqual(new[] { document }), "D8D legacy frozen derivation");
        var second = s.Dates[0] with { Date = monday.AddDays(4), Policy = s.Dates[0].Policy with { DocumentTypeId = Guid.NewGuid(), SupportingDocumentPolicy = "AlwaysRequired" } };
        check(LeaveEvidenceRules.RequiredTypes(s with { Dates = [s.Dates[0], second] }).Value!.Count == 2, "D8D multiple distinct required types");
        check(!LeaveEvidenceRules.RequiredTypes(s with { Dates = [s.Dates[0] with { Policy = s.Dates[0].Policy with { DocumentTypeId = null } }] }).IsSuccess, "D8D missing frozen document type conflict");
        check(!LeaveEvidenceRules.RequiredTypes(s with { SupportingDocumentRequired = false }).IsSuccess, "D8D inconsistent requirement conflict");
        for (int minutes = 1; minutes <= 12; minutes++)
        {
            var scheduled = new[] { new ScheduledLeaveInterval(Guid.NewGuid(), new(8,0), new(8,minutes)), new ScheduledLeaveInterval(Guid.NewGuid(), new(13,0), new(13,minutes)) };
            var full = s.Dates[0] with { ScheduledIntervals = scheduled, ChargedIntervals = scheduled.Select(x => new WorkIntervalDto(x.StartTime, x.EndTime)).ToArray(), ChargeableMinutes = minutes*2 };
            check(LeaveEvidenceRules.FullBoundary(full), "D8D complete variable split schedule " + minutes);
            check(!LeaveEvidenceRules.FullBoundary(full with { ChargedIntervals = [full.ChargedIntervals[0]] }), "D8D partial boundary rejected " + minutes);
        }
        check(!LeaveEvidenceRules.FullBoundary(s.Dates[0] with { ScheduledIntervals = [], ChargedIntervals = [] }), "D8D zero schedule is not boundary");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var field in new[] { "storageKey", "actorId", "verifiedAt", "recordedAt", "evidenceKind" })
        {
            bool rejected = false; try { JsonSerializer.Deserialize<LeaveEvidenceRequest>("{\"" + field + "\":null}", json); } catch (JsonException) { rejected = true; }
            check(rejected, "D8D forbidden receipt field " + field);
        }
        foreach (var value in new[] { "0", "1", "\"Charged\"", "\"Exempted\"" })
        {
            bool rejected = false; try { JsonSerializer.Deserialize<LeaveSandwichReviewRequest>("{\"expectedStatus\":" + value + "}", json); } catch (JsonException) { rejected = true; }
            check(rejected, "D8D strict review precondition " + value);
        }
        check(JsonSerializer.Deserialize<LeaveSandwichReviewRequest>("{\"expectedStatus\":\"ReviewPending\"}",json)!.ExpectedStatus == LeaveSandwichExpectedStatus.ReviewPending, "D8D typed ReviewPending contract");
        foreach (var outcome in new[] { "ReasonAccepted", "ReasonNotAccepted" })
            check(JsonSerializer.Deserialize<LeaveSandwichReviewRequest>("{\"outcome\":\"" + outcome + "\"}", json)!.Outcome!.Value.ToString() == outcome, "D8D explicit HR review outcome " + outcome);
        foreach (var value in new[] { "0", "1", "\"Charged\"", "\"Exempted\"", "\"Unknown\"" })
        {
            bool rejected = false; try { JsonSerializer.Deserialize<LeaveSandwichReviewRequest>("{\"outcome\":" + value + "}", json); } catch (JsonException) { rejected = true; }
            check(rejected, "D8D strict review outcome " + value);
        }
        foreach (var field in new[] { "actorId", "reviewedAt", "isExempt", "expectedState" })
        {
            bool rejected = false; try { JsonSerializer.Deserialize<LeaveSandwichReviewRequest>("{\"" + field + "\":null}", json); } catch (JsonException) { rejected = true; }
            check(rejected, "D8D server-owned review field " + field);
        }
        check(new EmployeeLeaveSandwichCase().State == LeaveSandwichState.ReviewPending, "D8D detection defaults to ReviewPending rather than an imposed debit");
        check(new EmployeeLeaveSandwichDate().ScheduledMinutes == 0, "D8D potential sandwich date does not fabricate work");
        foreach (var available in new long[] { 0, 1, 160, 600, 601, int.MaxValue })
            check(EmployeeLeaveService.CapSandwichDebit(600, available) == Math.Min(600, available), "D8D independent capped allocation " + available);
        check(new LeaveSandwichAllocationDto(2030, 600, 160).UnabsorbedDebitMinutes == 440, "D8D potential/applied/unabsorbed accounting");
        foreach (var payload in new[] { "{\"isPaid\":false}", "{\"balanceTracked\":false}" })
        {
            bool rejected = false; try { JsonSerializer.Deserialize<LeaveSandwichSnapshot>(payload, json); } catch (JsonException) { rejected = true; }
            check(rejected, "D8D missing frozen boolean cannot default to false " + payload);
        }
        var entities = new[] { typeof(EmployeeLeaveEvidence), typeof(EmployeeLeaveEvidenceEvent), typeof(EmployeeLeaveApprovalEvidence), typeof(EmployeeLeaveSandwichCase), typeof(EmployeeLeaveSandwichEvent), typeof(EmployeeLeaveSandwichDate), typeof(EmployeeLeaveSandwichAllocation) };
        foreach (var type in entities)
        {
            var mapped = model.FindEntityType(type)!;
            check(mapped.GetForeignKeys().All(f => f.DeleteBehavior == DeleteBehavior.NoAction), "D8D NoAction " + type.Name);
            check(mapped.GetProperties().All(p => p.Name != "StorageKey"), "D8D no storage identity " + type.Name);
        }
        check(model.FindEntityType(typeof(LeavePolicy))!.FindProperty(nameof(LeavePolicy.SandwichEquivalentDayMinutes))!.GetDefaultValueSql() is null, "D8D no SQL equivalent-minute default");
        check(model.FindEntityType(typeof(EmployeeLeaveEvidenceEvent))!.GetForeignKeys().All(f => f.Properties.All(p => p.Name != "ActorId")), "D8D actor FK deferred");
    }
}
