namespace SIAMIS.Application.Employees;

public sealed record AttendanceTimeInterval(DateTime StartUtc, DateTime EndUtc);
public sealed record AttendancePresenceInterval(DateTime StartUtc, DateTime EndUtc, Guid InEventId, Guid OutEventId);
public sealed record AttendanceDayFinding(string Code, string Message, IReadOnlyList<Guid> SourceIds);
public sealed record AttendanceLeaveEvidence(Guid LeaveId, string ObservedStatus, int SnapshotVersion, bool IsPaid, LeaveDateCalculation Date);

/// <summary>Current calculated facts, not a finalized historical attendance record. Null coverage totals mean partition is unavailable.</summary>
public sealed class AttendanceDayDto
{
    public Guid EmployeeId { get; init; }
    public DateOnly BusinessDate { get; init; }
    public string BusinessTimeZone { get; init; } = "Asia/Bangkok";
    public DateTime ObservedAtUtc { get; init; }
    public string Readiness { get; init; } = "Ready";
    public bool CoveragePartitionAvailable { get; init; }
    public bool PotentialAbsence { get; init; }
    public AttendanceExpectedWorkDto ExpectedWork { get; init; } = null!;
    public IReadOnlyList<AttendanceEventDto> Events { get; init; } = [];
    public IReadOnlyList<AttendanceLeaveEvidence> ApprovedLeaves { get; init; } = [];
    public IReadOnlyList<AttendanceDayFinding> Findings { get; init; } = [];
    public IReadOnlyList<AttendanceTimeInterval> ScheduledIntervals { get; init; } = [];
    public IReadOnlyList<AttendancePresenceInterval> ObservedPresenceIntervals { get; init; } = [];
    // Diagnostic intersections preserve both sources on conflict; they are not an authoritative partition then.
    public IReadOnlyList<AttendanceTimeInterval> PresenceCoveredScheduledIntervals { get; init; } = [];
    public IReadOnlyList<AttendanceTimeInterval> ApprovedLeaveCoveredIntervals { get; init; } = [];
    public IReadOnlyList<AttendanceTimeInterval> PresenceLeaveOverlapIntervals { get; init; } = [];
    public IReadOnlyList<AttendanceTimeInterval> UnexplainedScheduledIntervals { get; init; } = [];
    public long ObservedPresenceMilliseconds { get; init; }
    public long? ScheduledMilliseconds { get; init; }
    public long? PresenceCoveredScheduledMilliseconds { get; init; }
    public long? ApprovedLeaveCoveredScheduledMilliseconds { get; init; }
    public long? PaidLeaveCoveredMilliseconds { get; init; }
    public long? UnpaidLeaveCoveredMilliseconds { get; init; }
    public long? UnexplainedScheduledMilliseconds { get; init; }
    /// <summary>Conversion residue only; never attendance, Leave, absence or payroll time.</summary>
    public long? CoverageTruncationResidualMilliseconds { get; init; }
    public DateTime? ExpectedArrivalUtc { get; init; }
    public DateTime? FirstRelevantPresenceUtc { get; init; }
    public long? RawStartVarianceTicks { get; init; }
    public long? RawStartVarianceMilliseconds { get; init; }
    public bool? IsLateUnderCurrentPolicy { get; init; }
    public int ClockInGraceMinutes { get; init; } = 5;
    public string CalculationContractVersion { get; init; } = "D9C-v1";
    public string CurrentGracePolicy { get; init; } = "FiveMinuteClockInGrace-v1";
}
public interface IAttendanceDayService
{
    Task<ServiceResult<AttendanceDayDto>> GetAsync(Guid employeeId, DateOnly date, CancellationToken ct);
}
