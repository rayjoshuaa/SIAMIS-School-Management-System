using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using static SIAMIS.Infrastructure.Services.AttendanceReviewSources;

namespace SIAMIS.Infrastructure.Services;

public sealed class AttendanceReviewService(SIAMISDbContext db) : IAttendanceReviewService
{
    private sealed record View(AttendanceDayDto Raw, AttendanceDayDto Calculation, List<AttendanceReviewAction> Actions,
        AttendanceReviewCase? Case, FinalizedAttendanceRevision? Latest, AttendanceSourceSet Sources, string Hash, bool Reopened, bool Confirmed);
    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
    private static bool Concurrent(Exception e) => e is SqlException { Number: 1205 or 2601 or 2627 } || e.InnerException is not null && Concurrent(e.InnerException);
    private static FinalizedAttendanceRevisionDto Revision(FinalizedAttendanceRevision x) => new(x.Id, x.Revision, DateTime.SpecifyKind(x.FinalizedAtUtc, DateTimeKind.Utc),
        x.ActorUserId, x.SourceFingerprint, Parse<AttendanceFinalizedSnapshot>(x.SnapshotJson));
    private async Task<View> Load(Employee employee, DateOnly date, CancellationToken ct)
    {
        var raw = await AttendanceDayService.CalculateLockedAsync(db, employee, date, ct);
        var actions = await db.Set<AttendanceReviewAction>().AsNoTracking().Where(x => x.EmployeeId == employee.EmployeeId && x.BusinessDate == date).OrderBy(x => x.Sequence).ToListAsync(ct);
        var c = await db.Set<AttendanceReviewCase>().SingleOrDefaultAsync(x => x.EmployeeId == employee.EmployeeId && x.BusinessDate == date, ct);
        var latest = await db.Set<FinalizedAttendanceRevision>().AsNoTracking().Where(x => x.EmployeeId == employee.EmployeeId && x.BusinessDate == date).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        var calculated = Calculate(raw, actions); var sources = Sources(raw, actions); var hash = Fingerprint(sources);
        return new(raw, calculated, actions, c, latest, sources, hash, latest is not null && actions.Any(x => x.Action == "Reopened" && x.FinalizedRevisionId == latest.Id), Confirmed(calculated, hash, actions));
    }
    private async Task<AttendanceReviewDto> Response(View v, CancellationToken ct)
    {
        bool stale = v.Latest is not null && v.Latest.SourceFingerprint != v.Hash;
        IReadOnlyList<AttendanceDayFinding> changed = [];
        if (stale)
        {
            var previous = Parse<AttendanceSourceSet>(v.Latest!.SourcesJson);
            var statuses = await db.EmployeeLeaves.AsNoTracking().Where(x => x.EmployeeId == v.Raw.EmployeeId && previous.LeaveIds.Contains(x.LeaveId)).Select(x => new { x.LeaveId, x.Status }).ToDictionaryAsync(x => x.LeaveId, x => x.Status, ct);
            changed = Changes(previous, v.Sources, statuses);
        }
        return new()
        {
            EmployeeId = v.Raw.EmployeeId, BusinessDate = v.Raw.BusinessDate, Version = v.Actions.Select(x => x.Sequence).DefaultIfEmpty(0).Max(), SourceFingerprint = v.Hash,
            RawCalculation = v.Raw, Calculation = v.Calculation, ReviewCase = v.Case is null ? null : new(v.Case.Id, v.Case.State, DateTime.SpecifyKind(v.Case.OpenedAtUtc, DateTimeKind.Utc), v.Case.ActorUserId, Parse<AttendanceDayDto>(v.Case.OriginalCalculationJson)),
            History = v.Actions.Select(Dto).ToArray(), LatestHistoricalFinalizedRevision = v.Latest is null ? null : Revision(v.Latest),
            IsStale = stale, RequiresReopen = stale && !v.Reopened, IsReopened = v.Reopened, IsCurrentlyValidated = v.Latest is not null && !stale && !v.Reopened,
            IsConfirmedAbsent = v.Confirmed, ChangedSources = changed
        };
    }
    public async Task<ServiceResult<AttendanceReviewDto>> ReadAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        if (date == DateOnly.MinValue) return Fail<AttendanceReviewDto>("validation", "Business date must be 0001-01-02 or later.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var employee = await EmploymentIntegrity.LockAsync(db, employeeId, ct);
            if (employee is null) return Fail<AttendanceReviewDto>("not_found", "Employee was not found.");
            var response = await Response(await Load(employee, date, ct), ct); await tx.CommitAsync(ct);
            return ServiceResult<AttendanceReviewDto>.Success(response);
        }
        catch (Exception e) when (Concurrent(e) || e is JsonException) { return Fail<AttendanceReviewDto>("conflict", "Concurrent or inconsistent attendance sources; retry or inspect stored snapshot integrity."); }
    }
    public async Task<ServiceResult<IReadOnlyList<FinalizedAttendanceRevisionDto>>> HistoryAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct)) return Fail<IReadOnlyList<FinalizedAttendanceRevisionDto>>("not_found", "Employee was not found.");
        try
        {
            var rows = await db.Set<FinalizedAttendanceRevision>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.BusinessDate == date).OrderBy(x => x.Revision).ToListAsync(ct);
            return ServiceResult<IReadOnlyList<FinalizedAttendanceRevisionDto>>.Success(rows.Select(Revision).ToArray());
        }
        catch (JsonException) { return Fail<IReadOnlyList<FinalizedAttendanceRevisionDto>>("conflict", "Stored finalized snapshot is inconsistent; integrity review is required."); }
    }
    public async Task<ServiceResult<AttendanceReviewDto>> MutateAsync(Guid employeeId, DateOnly date, string action, AttendanceReviewRequest r, CancellationToken ct)
    {
        if (date == DateOnly.MinValue || r.ExpectedVersion is null or < 0 || r.ExpectedSourceFingerprint is null || !System.Text.RegularExpressions.Regex.IsMatch(r.ExpectedSourceFingerprint, "^[A-F0-9]{64}$")
            || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 2000 || action is not ("CorrectionAdded" or "Adjudication" or "AbsenceConfirmed" or "Finalized" or "Reopened"))
            return Fail<AttendanceReviewDto>("validation", "Valid date, expected version/source fingerprint and nonempty reason up to 2000 characters are required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var employee = await EmploymentIntegrity.LockAsync(db, employeeId, ct);
            if (employee is null) return Fail<AttendanceReviewDto>("not_found", "Employee was not found.");
            var v = await Load(employee, date, ct); long sequence = v.Actions.Select(x => x.Sequence).DefaultIfEmpty(0).Max();
            if (r.ExpectedVersion != sequence || r.ExpectedSourceFingerprint != v.Hash) return Fail<AttendanceReviewDto>("conflict", "Attendance sources or workflow version changed. Reload the review.");
            if (action == "Reopened")
            {
                if (v.Latest is null || v.Reopened) return Fail<AttendanceReviewDto>("conflict", "Exactly one existing, not-yet-reopened finalized revision is required.");
            }
            else if (v.Latest is not null && !v.Reopened) return Fail<AttendanceReviewDto>("conflict", "Attendance is finalized. Explicitly reopen before correction or refinalization.");
            Guid? eventId = null;
            if (action == "CorrectionAdded")
            {
                if (r is not AttendanceCorrectionRequest correction || !AttendanceFoundationResolver.TryInstant(correction.OccurredAt, out var utc)
                    || correction.Direction is not (AttendanceDirection.In or AttendanceDirection.Out) || correction.ManualRequestKey is null || correction.ManualRequestKey == Guid.Empty
                    || AttendanceFoundationResolver.BusinessDate(utc) != date)
                    return Fail<AttendanceReviewDto>("validation", "A same-business-date explicit-offset timestamp, In/Out direction and nonempty ManualRequestKey are required.");
                if (await db.AttendanceEvents.AsNoTracking().AnyAsync(x => x.ManualRequestKey == correction.ManualRequestKey, ct)) return Fail<AttendanceReviewDto>("conflict", "ManualRequestKey already identifies attendance evidence; no duplicate correction was added.");
                var e = new AttendanceEvent { EmployeeId = employeeId, BusinessDate = date, OccurredAtUtc = utc, Direction = correction.Direction.ToString()!,
                    ManualRequestKey = correction.ManualRequestKey, OriginalSourceTimestamp = correction.OccurredAt, Reason = r.Reason.Trim(),
                    EmployeeWasInactive = !employee.IsActive, EmploymentReadiness = v.Raw.ExpectedWork.Employment is null ? v.Raw.ExpectedWork.Readiness == "NotEmployed" ? "NotEmployed" : "ConfigurationConflict" : "Ready" };
                e.ReceivedAtUtc = DateTime.UtcNow; db.Add(e); eventId = e.AttendanceEventId;
            }
            if (action == "Adjudication")
            {
                if (r is not AttendanceAdjudicationRequest decision || decision.Included is null || decision.AttendanceEventId is null) return Fail<AttendanceReviewDto>("validation", "Event ID and explicit Included decision are required.");
                if (!v.Raw.Events.Any(x => x.AttendanceEventId == decision.AttendanceEventId)) return Fail<AttendanceReviewDto>("not_found", "Attendance evidence was not found for this employee/date.");
                eventId = decision.AttendanceEventId; action = decision.Included.Value ? "Included" : "Excluded";
            }
            if (action == "AbsenceConfirmed" && (!v.Calculation.PotentialAbsence || !v.Calculation.CoveragePartitionAvailable || v.Calculation.Findings.Any(x => x.Code != "PotentialAbsence")))
                return Fail<AttendanceReviewDto>("conflict", "Only unambiguous PotentialAbsence may be explicitly confirmed.");
            if (action == "Finalized" && !CanFinalize(v.Calculation, v.Confirmed)) return Fail<AttendanceReviewDto>("conflict", "Unresolved blocking findings prevent finalization; absence requires explicit confirmation.");
            var review = v.Case;
            if (action != "Finalized" && review is null)
            {
                review = new() { EmployeeId = employeeId, BusinessDate = date, OriginalCalculationJson = Serialize(v.Raw), OriginalSourceFingerprint = v.Hash };
                db.Add(review);
            }
            if (review is not null) review.State = action == "Finalized" ? "Resolved" : "Open";
            var audit = new AttendanceReviewAction { EmployeeId = employeeId, BusinessDate = date, Sequence = checked(sequence + 1), ReviewCaseId = review?.Id,
                Action = action, AttendanceEventId = eventId, Reason = r.Reason.Trim(), SourceFingerprint = v.Hash, CalculationJson = Serialize(v.Calculation) };
            if (action == "Reopened") audit.FinalizedRevisionId = v.Latest!.Id;
            if (action == "Finalized")
            {
                var c = v.Calculation;
                var revision = new FinalizedAttendanceRevision { EmployeeId = employeeId, BusinessDate = date, Revision = checked((v.Latest?.Revision ?? 0) + 1), ReviewCaseId = review?.Id,
                    IsConfirmedAbsent = v.Confirmed, IsLate = c.IsLateUnderCurrentPolicy, ScheduledMilliseconds = c.ScheduledMilliseconds!.Value,
                    PresenceCoveredScheduledMilliseconds = c.PresenceCoveredScheduledMilliseconds!.Value, ApprovedLeaveCoveredScheduledMilliseconds = c.ApprovedLeaveCoveredScheduledMilliseconds!.Value,
                    UnexplainedScheduledMilliseconds = c.UnexplainedScheduledMilliseconds!.Value, CoverageTruncationResidualMilliseconds = c.CoverageTruncationResidualMilliseconds!.Value,
                    SourceFingerprint = v.Hash, SourcesJson = Serialize(v.Sources), SnapshotJson = Serialize(new AttendanceFinalizedSnapshot(1, c, v.Raw.Events, v.Actions.Select(Dto).ToArray(), v.Confirmed)) };
                db.Add(revision); audit.FinalizedRevisionId = revision.Id;
            }
            db.Add(audit); await db.SaveChangesAsync(ct);
            var response = await Response(await Load(employee, date, ct), ct); await tx.CommitAsync(ct);
            return ServiceResult<AttendanceReviewDto>.Success(response);
        }
        catch (Exception e) when (Concurrent(e) || e is JsonException) { db.ChangeTracker.Clear(); return Fail<AttendanceReviewDto>("conflict", "Concurrent or inconsistent attendance state; reload the review. Nothing was partially applied."); }
    }
}
