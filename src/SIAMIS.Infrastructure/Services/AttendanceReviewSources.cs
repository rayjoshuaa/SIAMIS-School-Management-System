using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Infrastructure.Services;

public sealed record AttendanceSourceSet(string Work, string Evidence, string Leave, string Decisions, string Policy, string Integrity,
    IReadOnlyList<Guid> EventIds, IReadOnlyList<Guid> LeaveIds);

public static class AttendanceReviewSources
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Parse<T>(string value) => JsonSerializer.Deserialize<T>(value, Json) ?? throw new JsonException("Stored attendance snapshot is missing.");
    public static AttendanceReviewActionDto Dto(AttendanceReviewAction a) => new(a.Id, a.Sequence, a.ReviewCaseId, a.Action, a.AttendanceEventId,
        a.FinalizedRevisionId, a.Reason, DateTime.SpecifyKind(a.OccurredAtUtc, DateTimeKind.Utc), a.ActorUserId, a.Origin);
    public static AttendanceReviewAction[] Effective(IReadOnlyList<AttendanceReviewAction> actions) => actions.Where(x => x.Action is "Included" or "Excluded")
        .GroupBy(x => x.AttendanceEventId).Select(x => x.OrderByDescending(a => a.Sequence).First()).OrderBy(x => x.AttendanceEventId).ToArray();
    public static AttendanceDayDto Calculate(AttendanceDayDto raw, IReadOnlyList<AttendanceReviewAction> actions)
    {
        var excluded = Effective(actions).Where(x => x.Action == "Excluded").Select(x => x.AttendanceEventId).ToHashSet();
        return AttendanceDayCalculator.Calculate(raw.ExpectedWork, raw.Events.Where(x => !excluded.Contains(x.AttendanceEventId)).ToArray(),
            raw.ApprovedLeaves, raw.Findings.Where(x => x.Code == "LeaveSnapshotInvalid").ToArray(), raw.ObservedAtUtc);
    }
    public static AttendanceSourceSet Sources(AttendanceDayDto raw, IReadOnlyList<AttendanceReviewAction> actions)
    {
        var w = raw.ExpectedWork;
        return new(
            Serialize(new { w.EmployeeId, w.Date, w.BusinessTimeZone, w.Readiness, Employment = w.Employment is null ? null : new { w.Employment.EmploymentRecordId, w.Employment.EmploymentStart, w.Employment.EmploymentEnd },
                w.AssignmentId, w.WorkCalendarId, w.OverrideId, w.ScheduleKind, Intervals = w.Intervals.OrderBy(x => x.StartTime).ThenBy(x => x.IntervalId) }),
            Serialize(raw.Events.OrderBy(x => x.AttendanceEventId).Select(x => new { x.AttendanceEventId, x.OccurredAtUtc, x.Direction, x.Source, x.BusinessDate })),
            Serialize(raw.ApprovedLeaves.OrderBy(x => x.LeaveId).Select(x =>
            {
                // Keep V1 fingerprint bytes unchanged; only V2 adds classification sources.
                var fact = JsonSerializer.SerializeToNode(new { x.LeaveId, x.SnapshotVersion, x.IsPaid, x.Date.Date, x.Date.EmploymentRecordId, x.Date.AssignmentId, x.Date.WorkCalendarId,
                    x.Date.OverrideId, x.Date.ScheduleSource, x.Date.ScheduledIntervals, x.Date.ChargedIntervals }, Json)!;
                if (x.SnapshotVersion == 2) fact["paymentIntervals"] = JsonSerializer.SerializeToNode(x.Date.PaymentIntervals, Json);
                return fact;
            })),
            Serialize(new { Adjudications = Effective(actions).Select(x => new { x.Id, x.AttendanceEventId, x.Action }), Corrections = actions.Where(x => x.Action == "CorrectionAdded").OrderBy(x => x.Id).Select(x => new { x.Id, x.AttendanceEventId }) }),
            Serialize(new { raw.CalculationContractVersion, raw.CurrentGracePolicy, raw.ClockInGraceMinutes }),
            Serialize(raw.Findings.Where(x => x.Code == "LeaveSnapshotInvalid").OrderBy(x => x.Code).ThenBy(x => x.SourceIds.FirstOrDefault()).Select(x => new { x.Code, x.SourceIds })),
            raw.Events.Select(x => x.AttendanceEventId).OrderBy(x => x).ToArray(), raw.ApprovedLeaves.Select(x => x.LeaveId).OrderBy(x => x).ToArray());
    }
    public static string Fingerprint(AttendanceSourceSet sources) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(sources))));
    public static IReadOnlyList<AttendanceDayFinding> Changes(AttendanceSourceSet old, AttendanceSourceSet current, IReadOnlyDictionary<Guid, string> leaveStatuses)
    {
        var r = new List<AttendanceDayFinding>();
        if (old.Work != current.Work) r.Add(new("ExpectedWorkChanged", "Effective employment or assigned schedule sources changed after finalization.", []));
        if (old.Evidence != current.Evidence) r.Add(new("AttendanceEvidenceChanged", "Attendance evidence available for this date changed after finalization.", old.EventIds.Concat(current.EventIds).Distinct().OrderBy(x => x).ToArray()));
        if (old.Decisions != current.Decisions) r.Add(new("AttendanceDecisionsChanged", "Effective correction or event adjudication sources changed after finalization.", old.EventIds.Concat(current.EventIds).Distinct().OrderBy(x => x).ToArray()));
        if (old.Leave != current.Leave || old.Integrity != current.Integrity)
        {
            foreach (var id in old.LeaveIds.Except(current.LeaveIds))
                r.Add(new(leaveStatuses.GetValueOrDefault(id) == "Cancelled" ? "ApprovedLeaveCancelled" : "ApprovedLeaveNoLongerApplicable",
                    leaveStatuses.GetValueOrDefault(id) == "Cancelled" ? "Approved Leave used in this revision was later cancelled." : "Approved Leave used in this revision is no longer an applicable validated source.", [id]));
            r.Add(new("ApprovedLeaveSourcesChanged", "Approved Leave coverage or snapshot integrity changed after finalization.", old.LeaveIds.Concat(current.LeaveIds).Distinct().OrderBy(x => x).ToArray()));
        }
        if (old.Policy != current.Policy) r.Add(new("AttendancePolicyChanged", "The current attendance calculation/policy contract differs from the frozen revision.", []));
        return r;
    }
    public static bool Confirmed(AttendanceDayDto calculation, string fingerprint, IReadOnlyList<AttendanceReviewAction> actions)
    {
        long reopen = actions.Where(x => x.Action == "Reopened").Select(x => x.Sequence).DefaultIfEmpty(0).Max();
        return calculation.PotentialAbsence && actions.Any(x => x.Action == "AbsenceConfirmed" && x.Sequence > reopen && x.SourceFingerprint == fingerprint);
    }
    public static bool CanFinalize(AttendanceDayDto calculation, bool confirmed) => calculation.CoveragePartitionAvailable
        && calculation.Findings.All(x => x.Code == "PotentialAbsence" && confirmed);
}
