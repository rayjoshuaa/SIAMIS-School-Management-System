using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Leave;

namespace SIAMIS.Infrastructure.Services;

public sealed partial class EmployeeLeaveService
{
    private async Task<long> SandwichCommitted(Guid employee, Guid type, int year, CancellationToken ct)
        => await (from a in db.Set<EmployeeLeaveSandwichAllocation>() join c in db.Set<EmployeeLeaveSandwichCase>() on a.CaseId equals c.Id
            where c.EmployeeId == employee && c.LeaveTypeId == type && c.BalanceTracked && a.LeaveYear == year
                && (c.State == LeaveSandwichState.Reserved || c.State == LeaveSandwichState.ReasonNotAccepted || c.State == LeaveSandwichState.Charged) select (long)(a.AppliedDebitMinutes ?? a.SandwichDebitMinutes)).SumAsync(ct);

    public static int CapSandwichDebit(int potential, long available) => (int)Math.Min(potential, Math.Max(0L, available));

    private async Task<ServiceResult<bool>> FormSandwiches(Guid employee, Guid newLeaveId, CancellationToken ct)
    {
        var leaves = await db.EmployeeLeaves.AsNoTracking().Where(x => x.EmployeeId == employee && (x.Status == "Pending" || x.Status == "Approved")).OrderBy(x => x.LeaveId).ToListAsync(ct);
        var sources = new Dictionary<Guid, LeaveCalculationSnapshot>();
        foreach (var l in leaves)
        {
            var s = LeaveSnapshotIntegrity.Read(l, await Allocations(l.LeaveId, ct));
            if (!s.IsSuccess) return Fail<bool>("conflict", s.Failure!.Message);
            sources.Add(l.LeaveId, s.Value!);
        }
        var boundaries = sources.SelectMany(x => x.Value.Dates.Where(LeaveEvidenceRules.FullBoundary).Select(d => (Id: x.Key, Snapshot: x.Value, Date: d))).OrderBy(x => x.Date.Date).ToArray();
        for (int i = 1; i < boundaries.Length; i++)
        {
            var before = boundaries[i - 1]; var after = boundaries[i];
            // Formation is triggered only by the new request; never backfill historical Approved pairs.
            if (before.Id != newLeaveId && after.Id != newLeaveId || after.Date.Date.DayNumber - before.Date.Date.DayNumber < 2) continue;
            if (before.Snapshot.LeaveTypeId != after.Snapshot.LeaveTypeId || before.Snapshot.IsPaid != after.Snapshot.IsPaid
                || before.Snapshot.BalanceTracked != after.Snapshot.BalanceTracked
                || !before.Date.Policy.SandwichParticipation || !after.Date.Policy.SandwichParticipation) continue;
            if (before.Date.Policy.SandwichEquivalentDayMinutes is null or <= 0 || after.Date.Policy.SandwichEquivalentDayMinutes is null or <= 0)
                return Fail<bool>("conflict", "Participating boundary policy lacks explicit equivalent minutes; new valid policy coverage is required.");
            var gapStart = before.Date.Date.AddDays(1); var gapEnd = after.Date.Date.AddDays(-1);
            if (await db.Set<EmployeeLeaveSandwichCase>().AnyAsync(x => x.EmployeeId == employee && x.GapStart == gapStart && x.GapEnd == gapEnd && x.IsActive, ct)) continue;
            var gap = new List<LeaveDateCalculation>(); bool qualifies = true;
            for (int n = gapStart.DayNumber; n <= gapEnd.DayNumber; n++)
            {
                var date = DateOnly.FromDayNumber(n);
                var frozen = sources.Values.Where(s => s.LeaveTypeId == before.Snapshot.LeaveTypeId).SelectMany(s => s.Dates).Where(d => d.Date == date).ToArray();
                LeaveDateCalculation fact;
                if (frozen.Length > 0)
                {
                    // Charged intersections are request-specific; resolution facts must match independently.
                    string Resolution(LeaveDateCalculation d) => JsonSerializer.Serialize(d with { ChargedIntervals = [], ChargeableMinutes = 0 }, SnapshotJson);
                    if (frozen.Skip(1).Any(d => Resolution(d) != Resolution(frozen[0]))) return Fail<bool>("conflict", "Frozen gap facts conflict; integrity review is required.");
                    fact = frozen[0] with { ChargedIntervals = [], ChargeableMinutes = 0 };
                }
                else
                {
                    var resolved = await ResolveGap(employee, before.Snapshot.LeaveTypeId, date, ct);
                    if (!resolved.IsSuccess) return Fail<bool>("conflict", resolved.Failure!.Message);
                    fact = resolved.Value!;
                }
                if (fact.ScheduledIntervals.Count != 0) { qualifies = false; break; }
                if (!fact.Policy.SandwichParticipation || fact.Policy.BalanceTracked != before.Snapshot.BalanceTracked) { qualifies = false; break; }
                if (fact.Policy.SandwichEquivalentDayMinutes is null or <= 0) return Fail<bool>("conflict", "Participating gap policy lacks explicit equivalent minutes; new valid policy coverage is required.");
                gap.Add(fact);
            }
            if (!qualifies) continue;
            var debits = gap.Select(d => new LeaveSandwichDateDto(d.Date, 0, d.Policy.SandwichEquivalentDayMinutes!.Value)).ToArray();
            var sums = debits.GroupBy(d => d.Date.Year).OrderBy(g => g.Key).Select(g => (Year: g.Key, Minutes: g.Sum(d => (long)d.SandwichDebitMinutes))).ToArray();
            if (sums.Any(g => g.Minutes > int.MaxValue)) return Fail<bool>("conflict", "Sandwich debit exceeds supported precision.");
            var allocations = sums.Select(g => new LeaveSandwichAllocationDto(g.Year, (int)g.Minutes)).ToArray();
            var revision = (await db.Set<EmployeeLeaveSandwichCase>().Where(x => x.EmployeeId == employee && x.GapStart == gapStart && x.GapEnd == gapEnd).Select(x => (int?)x.Revision).MaxAsync(ct) ?? 0) + 1;
            var c = new EmployeeLeaveSandwichCase { EmployeeId = employee, LeaveTypeId = before.Snapshot.LeaveTypeId, BeforeLeaveId = before.Id, AfterLeaveId = after.Id,
                GapStart = gapStart, GapEnd = gapEnd, Revision = revision, IsPaid = before.Snapshot.IsPaid!.Value, BalanceTracked = before.Snapshot.BalanceTracked };
            c.CalculationSnapshotJson = JsonSerializer.Serialize(new LeaveSandwichSnapshot(1, employee, c.LeaveTypeId, before.Id, after.Id, c.IsPaid, c.BalanceTracked,
                c.DetectedAt, before.Date, after.Date, gap, debits, allocations), SnapshotJson);
            db.Add(c); db.Add(new EmployeeLeaveSandwichEvent { CaseId = c.Id, State = LeaveSandwichState.ReviewPending, CausingLeaveId = newLeaveId });
            foreach (var d in debits) db.Add(new EmployeeLeaveSandwichDate { CaseId = c.Id, Date = d.Date, SandwichDebitMinutes = d.SandwichDebitMinutes });
            foreach (var a in allocations) db.Add(new EmployeeLeaveSandwichAllocation { CaseId = c.Id, LeaveYear = a.LeaveYear, SandwichDebitMinutes = a.SandwichDebitMinutes });
            await db.SaveChangesAsync(ct);
        }
        return ServiceResult<bool>.Success(true);
    }

    private async Task<ServiceResult<LeaveDateCalculation>> ResolveGap(Guid employee, Guid type, DateOnly date, CancellationToken ct)
    {
        ServiceResult<LeaveDateCalculation> Invalid(string m) => Fail<LeaveDateCalculation>("conflict", m);
        var employment = await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == employee).Where(EmploymentIntegrity.EffectiveOn(date)).ToListAsync(ct);
        if (employment.Count != 1 || EmploymentIntegrity.Dates(employment[0]) is not null) return Invalid("Sandwich gap employment coverage is missing or inconsistent.");
        var assignments = await db.Set<EmployeeWorkCalendarAssignment>().AsNoTracking().Where(x => x.EmployeeId == employee && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).ToListAsync(ct);
        if (assignments.Count != 1) return Invalid("Sandwich gap requires exactly one explicit work calendar assignment.");
        var a = assignments[0]; var calendar = await db.Set<WorkCalendar>().AsNoTracking().SingleAsync(x => x.Id == a.WorkCalendarId, ct);
        var policies = await db.Set<LeavePolicy>().AsNoTracking().Where(x => x.LeaveTypeId == type && x.Status == "Published" && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).ToListAsync(ct);
        if (policies.Count != 1) return Invalid("Sandwich gap requires exactly one Published policy.");
        var o = await db.Set<WorkCalendarDateOverride>().AsNoTracking().SingleOrDefaultAsync(x => x.WorkCalendarId == a.WorkCalendarId && x.Date == date, ct);
        var schedule = o is not null
            ? await db.Set<WorkCalendarOverrideInterval>().AsNoTracking().Where(x => x.WorkCalendarDateOverrideId == o.Id).OrderBy(x => x.StartTime).Select(x => new ScheduledLeaveInterval(x.Id, x.StartTime, x.EndTime)).ToArrayAsync(ct)
            : await db.Set<WorkCalendarWeeklyInterval>().AsNoTracking().Where(x => x.WorkCalendarId == a.WorkCalendarId && x.DayOfWeek == date.DayOfWeek).OrderBy(x => x.StartTime).Select(x => new ScheduledLeaveInterval(x.Id, x.StartTime, x.EndTime)).ToArrayAsync(ct);
        if (o is not null && (o.OverrideType == "ExceptionalWorkingDay" ? schedule.Length == 0 : schedule.Length != 0)
            || schedule.Any(x => x.StartTime >= x.EndTime || !LeaveRequestCalculator.WholeMinute(x.StartTime) || !LeaveRequestCalculator.WholeMinute(x.EndTime))
            || schedule.Skip(1).Where((x, i) => x.StartTime < schedule[i].EndTime).Any()) return Invalid("Sandwich gap schedule configuration is inconsistent.");
        return ServiceResult<LeaveDateCalculation>.Success(new(date, employment[0].EmploymentRecordId, a.Id, calendar.Id, calendar.Code, calendar.Name,
            o?.OverrideType ?? "Weekly", o?.Id, LeaveRequestCalculator.Evidence(policies[0]), schedule, [], 0));
    }

    private async Task<ServiceResult<bool>> ReconcileSandwiches(Guid employee, Guid changedLeave, string target, CancellationToken ct)
    {
        var cases = await db.Set<EmployeeLeaveSandwichCase>().Where(x => x.EmployeeId == employee && x.IsActive && (x.BeforeLeaveId == changedLeave || x.AfterLeaveId == changedLeave)).OrderBy(x => x.Id).ToListAsync(ct);
        foreach (var c in cases)
        {
            var valid = await ReadCase(c, ct); if (!valid.IsSuccess) return Fail<bool>("conflict", valid.Failure!.Message);
            if (target == "Approved" && (c.State == LeaveSandwichState.Reserved || c.State == LeaveSandwichState.ReasonNotAccepted) && c.BalanceTracked)
                foreach (var allocation in valid.Value!.Allocations)
                {
                    var balance = await AvailableAsync(employee, c.LeaveTypeId, allocation.LeaveYear, ct);
                    if (!balance.IsSuccess || balance.Value < 0) return Fail<bool>("conflict", "The sandwich reservation is inconsistent with its entitlement.");
                }
            LeaveSandwichState? next = null;
            if (target is "Cancelled" or "Rejected") next = LeaveSandwichState.Released;
            else if (target == "Approved")
            {
                var ids = new[] { c.BeforeLeaveId, c.AfterLeaveId };
                var states = await db.EmployeeLeaves.AsNoTracking().Where(x => ids.Contains(x.LeaveId)).Select(x => new { x.LeaveId, x.Status }).ToListAsync(ct);
                if (states.All(x => x.LeaveId == changedLeave || x.Status == "Approved"))
                {
                    if (c.State == LeaveSandwichState.ReviewPending)
                        return Fail<bool>("conflict", "Sandwich review must be completed before final boundary approval.");
                    if (c.State is LeaveSandwichState.ReasonNotAccepted or LeaveSandwichState.Reserved) next = LeaveSandwichState.Charged;
                }
            }
            if (!next.HasValue) continue;
            c.State = next.Value; c.IsActive = next != LeaveSandwichState.Released;
            db.Add(new EmployeeLeaveSandwichEvent { CaseId = c.Id, State = c.State, CausingLeaveId = changedLeave, Reason = target is "Cancelled" or "Rejected" ? "Boundary " + target : null });
        }
        return ServiceResult<bool>.Success(true);
    }
    private async Task<ServiceResult<LeaveSandwichSnapshot>> ReadCase(EmployeeLeaveSandwichCase c, CancellationToken ct)
    {
        ServiceResult<LeaveSandwichSnapshot> Invalid() => Fail<LeaveSandwichSnapshot>("conflict", "Frozen sandwich evidence is inconsistent; integrity review is required.");
        try
        {
            var s = JsonSerializer.Deserialize<LeaveSandwichSnapshot>(c.CalculationSnapshotJson, SnapshotJson);
            if (s is null || s.Version != 1 || s.EmployeeId != c.EmployeeId || s.LeaveTypeId != c.LeaveTypeId || s.BeforeLeaveId != c.BeforeLeaveId || s.AfterLeaveId != c.AfterLeaveId
                || s.IsPaid != c.IsPaid || s.BalanceTracked != c.BalanceTracked || s.ObservedAt != c.DetectedAt
                || s.Before.Date.AddDays(1) != c.GapStart || s.After.Date.AddDays(-1) != c.GapEnd
                || !LeaveEvidenceRules.FullBoundary(s.Before) || !LeaveEvidenceRules.FullBoundary(s.After)
                || !s.Before.Policy.SandwichParticipation || !s.After.Policy.SandwichParticipation || s.GapDates.Count != c.GapEnd.DayNumber - c.GapStart.DayNumber + 1) return Invalid();
            // Match the original immutable request provenance, never today's calendar/policy.
            foreach (var boundary in new[] { (Id: c.BeforeLeaveId, Fact: s.Before), (Id: c.AfterLeaveId, Fact: s.After) })
            {
                var header = await db.EmployeeLeaves.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == c.EmployeeId && x.LeaveId == boundary.Id, ct);
                if (header is null) return Invalid();
                var source = LeaveSnapshotIntegrity.Read(header, await Allocations(boundary.Id, ct));
                if (!source.IsSuccess || source.Value!.LeaveTypeId != c.LeaveTypeId || source.Value.IsPaid != c.IsPaid || source.Value.BalanceTracked != c.BalanceTracked
                    || JsonSerializer.Serialize(source.Value.Dates.SingleOrDefault(d => d.Date == boundary.Fact.Date), SnapshotJson) != JsonSerializer.Serialize(boundary.Fact, SnapshotJson)) return Invalid();
            }
            for (int i = 0; i < s.GapDates.Count; i++)
            {
                var d = s.GapDates[i];
                if (d.Date.DayNumber != c.GapStart.DayNumber + i || d.ScheduledIntervals.Count != 0 || d.ChargedIntervals.Count != 0 || d.ChargeableMinutes != 0
                    || d.EmploymentRecordId == Guid.Empty || d.AssignmentId == Guid.Empty || d.WorkCalendarId == Guid.Empty
                    || d.ScheduleSource is not ("Weekly" or "PublicHoliday" or "SchoolHoliday" or "RestDay")
                    || (d.ScheduleSource == "Weekly" ? d.OverrideId.HasValue : !d.OverrideId.HasValue)
                    || !d.Policy.SandwichParticipation || d.Policy.BalanceTracked != c.BalanceTracked || d.Policy.Id == Guid.Empty
                    || string.IsNullOrWhiteSpace(d.Policy.Version) || d.Policy.SandwichEquivalentDayMinutes is null or <= 0) return Invalid();
            }
            var debits = s.GapDates.Select(d => new LeaveSandwichDateDto(d.Date, 0, d.Policy.SandwichEquivalentDayMinutes!.Value)).ToArray();
            var allocations = debits.GroupBy(d => d.Date.Year).OrderBy(g => g.Key).Select(g => new LeaveSandwichAllocationDto(g.Key, checked((int)g.Sum(d => (long)d.SandwichDebitMinutes)))).ToArray();
            var actualDates = await db.Set<EmployeeLeaveSandwichDate>().AsNoTracking().Where(x => x.CaseId == c.Id).OrderBy(x => x.Date).Select(x => new LeaveSandwichDateDto(x.Date, x.ScheduledMinutes, x.SandwichDebitMinutes)).ToListAsync(ct);
            var actualAlloc = await db.Set<EmployeeLeaveSandwichAllocation>().AsNoTracking().Where(x => x.CaseId == c.Id).OrderBy(x => x.LeaveYear).Select(x => new LeaveSandwichAllocationDto(x.LeaveYear, x.SandwichDebitMinutes)).ToListAsync(ct);
            var applied = await db.Set<EmployeeLeaveSandwichAllocation>().AsNoTracking().Where(x => x.CaseId == c.Id).Select(x => x.AppliedDebitMinutes).ToListAsync(ct);
            var reviewed = await db.Set<EmployeeLeaveSandwichEvent>().AsNoTracking().AnyAsync(x => x.CaseId == c.Id && (x.State == LeaveSandwichState.ReasonAccepted || x.State == LeaveSandwichState.ReasonNotAccepted), ct);
            if (c.State == LeaveSandwichState.ReviewPending && applied.Any(x => x.HasValue)
                || c.State == LeaveSandwichState.ReasonAccepted && applied.Any(x => x != 0)
                || (reviewed || c.State is LeaveSandwichState.ReasonAccepted or LeaveSandwichState.ReasonNotAccepted) && applied.Any(x => !x.HasValue)) return Invalid();
            return debits.SequenceEqual(s.Debits) && allocations.SequenceEqual(s.Allocations) && debits.SequenceEqual(actualDates) && allocations.SequenceEqual(actualAlloc)
                ? ServiceResult<LeaveSandwichSnapshot>.Success(s) : Invalid();
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or NullReferenceException or OverflowException) { return Invalid(); }
    }
    private async Task<LeaveSandwichDto> SandwichDto(EmployeeLeaveSandwichCase c, LeaveSandwichSnapshot s, CancellationToken ct)
    {
        var events = await db.Set<EmployeeLeaveSandwichEvent>().AsNoTracking().Where(x => x.CaseId == c.Id).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToListAsync(ct);
        var review = events.LastOrDefault(x => x.State is LeaveSandwichState.ReasonAccepted or LeaveSandwichState.ReasonNotAccepted);
        var allocations = await db.Set<EmployeeLeaveSandwichAllocation>().AsNoTracking().Where(x => x.CaseId == c.Id).OrderBy(x => x.LeaveYear)
            .Select(x => new LeaveSandwichAllocationDto(x.LeaveYear, x.SandwichDebitMinutes, x.AppliedDebitMinutes)).ToListAsync(ct);
        long applied = allocations.Sum(x => (long)(x.AppliedDebitMinutes ?? (c.State is LeaveSandwichState.Reserved or LeaveSandwichState.Charged ? x.SandwichDebitMinutes : 0)));
        return new(c.Id, c.EmployeeId, c.LeaveTypeId, c.BeforeLeaveId, c.AfterLeaveId, c.Revision, c.State.ToString(), c.IsPaid, c.BalanceTracked, AsUtc(c.DetectedAt),
            s.Debits.Sum(d => (long)d.SandwichDebitMinutes), s.Debits, allocations, events.Select(x => new LeaveSandwichEventDto(x.Id, x.State.ToString(), x.CausingLeaveId, x.Reason, AsUtc(x.OccurredAt))).ToArray())
        {
            AppliedSandwichDebitMinutes = applied,
            CommittedDebitMinutes = c.BalanceTracked && (c.State is LeaveSandwichState.ReasonNotAccepted or LeaveSandwichState.Reserved or LeaveSandwichState.Charged) ? applied : 0,
            ReviewOutcome = review?.State.ToString(), ReviewedAt = review is null ? null : AsUtc(review.OccurredAt)
        };
    }
    public async Task<ServiceResult<IReadOnlyList<LeaveSandwichDto>>> SandwichesAsync(Guid employeeId, Guid? leaveId, CancellationToken ct)
    {
        if (!await Exists(employeeId, ct)) return Fail<IReadOnlyList<LeaveSandwichDto>>("not_found", "Employee was not found.");
        var cases = await db.Set<EmployeeLeaveSandwichCase>().AsNoTracking().Where(x => x.EmployeeId == employeeId && (!leaveId.HasValue || x.BeforeLeaveId == leaveId || x.AfterLeaveId == leaveId)).OrderBy(x => x.GapStart).ThenBy(x => x.Revision).ToListAsync(ct);
        var result = new List<LeaveSandwichDto>(); foreach (var c in cases)
        {
            var valid = await ReadCase(c, ct); if (!valid.IsSuccess) return Fail<IReadOnlyList<LeaveSandwichDto>>("conflict", valid.Failure!.Message);
            result.Add(await SandwichDto(c, valid.Value!, ct));
        }
        return ServiceResult<IReadOnlyList<LeaveSandwichDto>>.Success(result);
    }
    public async Task<ServiceResult<LeaveSandwichDto>> ReviewSandwichAsync(Guid employeeId, Guid caseId, LeaveSandwichReviewRequest r, CancellationToken ct)
    {
        if (r.ExpectedStatus != LeaveSandwichExpectedStatus.ReviewPending || r.Outcome is null || !Enum.IsDefined(r.Outcome.Value) || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 2000)
            return Fail<LeaveSandwichDto>("validation", "ExpectedStatus=ReviewPending, an explicit review Outcome, and a reason up to 2000 characters are required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail<LeaveSandwichDto>("not_found", "Employee was not found.");
            var c = await db.Set<EmployeeLeaveSandwichCase>().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.Id == caseId, ct);
            if (c is null) return Fail<LeaveSandwichDto>("not_found", "Sandwich case was not found for this employee.");
            if (c.State != LeaveSandwichState.ReviewPending) return Fail<LeaveSandwichDto>("conflict", "Sandwich case no longer matches ExpectedStatus=ReviewPending.");
            var valid = await ReadCase(c, ct); if (!valid.IsSuccess) return Fail<LeaveSandwichDto>("conflict", valid.Failure!.Message);
            var amounts = new Dictionary<int, int>();
            foreach (var allocation in valid.Value!.Allocations)
            {
                int applied = 0;
                if (r.Outcome == LeaveSandwichReviewOutcome.ReasonNotAccepted && c.BalanceTracked)
                {
                    var balance = await AvailableAsync(employeeId, c.LeaveTypeId, allocation.LeaveYear, ct);
                    if (!balance.IsSuccess || balance.Value < 0)
                        return Fail<LeaveSandwichDto>("conflict", balance.IsSuccess ? "Existing entitlement commitments are inconsistent." : balance.Failure!.Message);
                    applied = CapSandwichDebit(allocation.SandwichDebitMinutes, balance.Value);
                }
                amounts.Add(allocation.LeaveYear, applied);
            }
            var allocations = await db.Set<EmployeeLeaveSandwichAllocation>().Where(x => x.CaseId == c.Id).ToListAsync(ct);
            foreach (var allocation in allocations) allocation.AppliedDebitMinutes = amounts[allocation.LeaveYear];
            c.State = r.Outcome == LeaveSandwichReviewOutcome.ReasonAccepted ? LeaveSandwichState.ReasonAccepted : LeaveSandwichState.ReasonNotAccepted;
            db.Add(new EmployeeLeaveSandwichEvent { CaseId = c.Id, State = c.State, Reason = r.Reason.Trim() });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ServiceResult<LeaveSandwichDto>.Success(await SandwichDto(c, valid.Value!, ct));
        }
        catch (Exception e) when (Concurrent(e)) { return Fail<LeaveSandwichDto>("conflict", "Sandwich case changed concurrently. Retry the request."); }
    }
}
