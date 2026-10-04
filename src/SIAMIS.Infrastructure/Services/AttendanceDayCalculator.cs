using SIAMIS.Application.Employees;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Pure exact-instant coverage. Whole milliseconds are independently truncated aggregate displays.</summary>
public static class AttendanceDayCalculator
{
    public static IReadOnlyList<AttendanceTimeInterval> Union(IEnumerable<AttendanceTimeInterval> source)
    {
        var result = new List<AttendanceTimeInterval>();
        foreach (var x in source.Where(x => x.EndUtc > x.StartUtc).OrderBy(x => x.StartUtc).ThenBy(x => x.EndUtc))
        {
            if (result.Count == 0 || result[^1].EndUtc < x.StartUtc) result.Add(x);
            else if (x.EndUtc > result[^1].EndUtc) result[^1] = result[^1] with { EndUtc = x.EndUtc };
        }
        return result;
    }
    public static IReadOnlyList<AttendanceTimeInterval> Intersect(IEnumerable<AttendanceTimeInterval> a, IEnumerable<AttendanceTimeInterval> b)
        => Union(a.SelectMany(x => b.Select(y => new AttendanceTimeInterval(x.StartUtc > y.StartUtc ? x.StartUtc : y.StartUtc,
            x.EndUtc < y.EndUtc ? x.EndUtc : y.EndUtc))));
    public static IReadOnlyList<AttendanceTimeInterval> Subtract(IEnumerable<AttendanceTimeInterval> a, IEnumerable<AttendanceTimeInterval> b)
    {
        var result = new List<AttendanceTimeInterval>(); var cuts = Union(b);
        foreach (var x in Union(a))
        {
            var cursor = x.StartUtc;
            foreach (var y in cuts.Where(y => y.EndUtc > x.StartUtc && y.StartUtc < x.EndUtc))
            {
                if (y.StartUtc > cursor) result.Add(new(cursor, y.StartUtc));
                if (y.EndUtc > cursor) cursor = y.EndUtc;
                if (cursor >= x.EndUtc) break;
            }
            if (cursor < x.EndUtc) result.Add(new(cursor, x.EndUtc));
        }
        return result;
    }
    public static long Ticks(IEnumerable<AttendanceTimeInterval> intervals) => Union(intervals).Sum(x => x.EndUtc.Ticks - x.StartUtc.Ticks);
    private static long Ms(IEnumerable<AttendanceTimeInterval> intervals) => Ticks(intervals) / TimeSpan.TicksPerMillisecond;
    private static DateTime Instant(DateOnly date, TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified),
        TimeZoneInfo.FindSystemTimeZoneById(AttendanceFoundationResolver.BusinessTimeZone));

    public static AttendanceDayDto Calculate(AttendanceExpectedWorkDto work, IReadOnlyList<AttendanceEventDto> events,
        IReadOnlyList<AttendanceLeaveEvidence> leaves, IReadOnlyList<AttendanceDayFinding> inputFindings, DateTime observedAtUtc)
    {
        var findings = new List<AttendanceDayFinding>(inputFindings);
        void Find(string code, string message, params Guid[] ids) => findings.Add(new(code, message, ids));
        var presence = new List<AttendancePresenceInterval>(); AttendanceEventDto? open = null; bool ambiguous = false;
        var ordered = events.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.AttendanceEventId).ToArray();
        foreach (var group in ordered.GroupBy(x => x.OccurredAtUtc))
        {
            if (group.Count() > 1)
            {
                Find("ExactTimestampConflict", "Same-instant events cannot establish a temporal ordering.", group.Select(x => x.AttendanceEventId).Concat(open is null ? [] : new[] { open.AttendanceEventId }).ToArray());
                open = null; ambiguous = true; continue;
            }
            var e = group.Single();
            if (e.Direction == "In")
            {
                if (open is not null || ambiguous) { Find("DuplicateDirection", "A usable IN/OUT pair cannot be chosen without guessing.", e.AttendanceEventId); ambiguous = true; }
                else open = e;
            }
            else if (e.Direction == "Out")
            {
                if (open is not null && !ambiguous) presence.Add(new(open.OccurredAtUtc, e.OccurredAtUtc, open.AttendanceEventId, e.AttendanceEventId));
                else Find("MissingClockIn", "OUT has no unambiguous preceding IN.", e.AttendanceEventId);
                open = null; ambiguous = false;
            }
            else { Find("UnknownDirection", "Unknown evidence prevents authoritative pairing through this point.", e.AttendanceEventId); open = null; ambiguous = true; }
        }
        if (open is not null || ambiguous) Find("MissingClockOut", "Evidence ends without an unambiguous closing OUT.", open is null ? [] : new[] { open.AttendanceEventId });
        if (work.Readiness != "Ready") Find(work.Readiness, work.Finding ?? "Expected work is unavailable.");
        var schedule = work.Readiness == "Ready" ? Union(work.Intervals.Select(x => new AttendanceTimeInterval(Instant(work.Date, x.StartTime), Instant(work.Date, x.EndTime)))) : [];
        foreach (var l in leaves)
        {
            var d = l.Date;
            if (d.EmploymentRecordId != work.Employment?.EmploymentRecordId || d.AssignmentId != work.AssignmentId || d.WorkCalendarId != work.WorkCalendarId
                || d.OverrideId != work.OverrideId || !d.ScheduledIntervals.Select(x => (x.Id, x.StartTime, x.EndTime)).SequenceEqual(work.Intervals.Select(x => (x.IntervalId, x.StartTime, x.EndTime))))
                Find("LeaveScheduleMismatch", "Frozen Leave schedule differs from current expected work; no precedence selected.", l.LeaveId);
        }
        IReadOnlyList<AttendanceTimeInterval> Charged(AttendanceLeaveEvidence l) => l.Date.ChargedIntervals.Select(x => new AttendanceTimeInterval(Instant(work.Date, x.StartTime), Instant(work.Date, x.EndTime))).ToArray();
        for (int i = 0; i < leaves.Count; i++)
            for (int j = i + 1; j < leaves.Count; j++)
                if (Intersect(Charged(leaves[i]), Charged(leaves[j])).Count > 0)
                    Find("ApprovedLeaveOverlap", "Stored Approved Leave intervals overlap; integrity review is required.", leaves[i].LeaveId, leaves[j].LeaveId);
        var observed = Union(presence.Select(x => new AttendanceTimeInterval(x.StartUtc, x.EndUtc)));
        var p = Intersect(schedule, observed); var lcov = Intersect(schedule, Union(leaves.SelectMany(Charged)));
        var overlap = Intersect(p, lcov);
        if (overlap.Count > 0) Find("PresenceLeaveOverlap", "Presence and Approved Leave overlap; neither source takes precedence.", leaves.Select(x => x.LeaveId).ToArray());
        var u = Subtract(schedule, Union(p.Concat(lcov)));
        bool partition = findings.Count == 0;
        bool absence = partition && Ticks(schedule) > 0 && Ticks(p) == 0 && Ticks(lcov) == 0;
        if (absence) Find("PotentialAbsence", "All scheduled work remains unexplained. This is not confirmed absence.");
        var required = Subtract(schedule, lcov);
        // Overlap blocks coverage totals, but need not hide unambiguous arrival facts. Approved morning Leave still shifts the boundary.
        bool arrivalAvailable = findings.All(x => x.Code is "PresenceLeaveOverlap" or "PotentialAbsence");
        DateTime? arrival = arrivalAvailable && required.Count > 0 ? required[0].StartUtc : null;
        var first = arrival.HasValue ? presence.FirstOrDefault(x => x.EndUtc > arrival.Value) : null;
        long? variance = first is null ? null : Math.Max(0, first.StartUtc.Ticks - arrival!.Value.Ticks);
        if (partition && Ticks(schedule) != Ticks(p) + Ticks(lcov) + Ticks(u)) throw new InvalidOperationException("Exact coverage partition invariant failed.");
        long? residual = partition ? Ms(schedule) - Ms(p) - Ms(lcov) - Ms(u) : null;
        if (residual < 0) throw new InvalidOperationException("Coverage truncation residual cannot be negative.");
        return new()
        {
            EmployeeId = work.EmployeeId, BusinessDate = work.Date, BusinessTimeZone = work.BusinessTimeZone, ObservedAtUtc = observedAtUtc,
            ExpectedWork = work, Readiness = work.Readiness != "Ready" ? work.Readiness : findings.Count == 0 ? "Ready" : "RequiresReview",
            CoveragePartitionAvailable = partition, PotentialAbsence = absence, Events = ordered, ApprovedLeaves = leaves, Findings = findings,
            ScheduledIntervals = schedule, ObservedPresenceIntervals = presence, PresenceCoveredScheduledIntervals = p,
            ApprovedLeaveCoveredIntervals = lcov, PresenceLeaveOverlapIntervals = overlap, UnexplainedScheduledIntervals = u,
            ObservedPresenceMilliseconds = Ms(observed), ScheduledMilliseconds = work.Readiness == "Ready" ? Ms(schedule) : null,
            PresenceCoveredScheduledMilliseconds = partition ? Ms(p) : null, ApprovedLeaveCoveredScheduledMilliseconds = partition ? Ms(lcov) : null,
            PaidLeaveCoveredMilliseconds = partition ? Ms(Intersect(schedule, leaves.Where(x => x.IsPaid).SelectMany(Charged))) : null,
            UnpaidLeaveCoveredMilliseconds = partition ? Ms(Intersect(schedule, leaves.Where(x => !x.IsPaid).SelectMany(Charged))) : null,
            UnexplainedScheduledMilliseconds = partition ? Ms(u) : null, CoverageTruncationResidualMilliseconds = residual,
            ExpectedArrivalUtc = arrival, FirstRelevantPresenceUtc = first?.StartUtc, RawStartVarianceTicks = variance,
            RawStartVarianceMilliseconds = variance / TimeSpan.TicksPerMillisecond, IsLateUnderCurrentPolicy = variance.HasValue ? variance > 5 * TimeSpan.TicksPerMinute : null
        };
    }
}
