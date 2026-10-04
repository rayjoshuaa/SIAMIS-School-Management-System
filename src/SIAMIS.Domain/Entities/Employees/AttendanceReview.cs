namespace SIAMIS.Domain.Entities.Employees;

public sealed class AttendanceReviewCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string State { get; set; } = "Open";
    public string OriginalCalculationJson { get; set; } = string.Empty;
    public string OriginalSourceFingerprint { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ActorUserId { get; set; }
}

/// <summary>Append-only attendance decisions. ActorUserId is null for unattributed Development operations.</summary>
public sealed class AttendanceReviewAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public long Sequence { get; set; }
    public Guid? ReviewCaseId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid? AttendanceEventId { get; set; }
    public Guid? FinalizedRevisionId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SourceFingerprint { get; set; } = string.Empty;
    public string CalculationJson { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ActorUserId { get; set; }
    public string Origin { get; set; } = "DevelopmentUnattributed";
}

/// <summary>Immutable historical calculation; source changes affect validity metadata, never this payload.</summary>
public sealed class FinalizedAttendanceRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public int Revision { get; set; }
    public Guid? ReviewCaseId { get; set; }
    public DateTime FinalizedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ActorUserId { get; set; }
    public bool IsConfirmedAbsent { get; set; }
    public bool? IsLate { get; set; }
    public long ScheduledMilliseconds { get; set; }
    public long PresenceCoveredScheduledMilliseconds { get; set; }
    public long ApprovedLeaveCoveredScheduledMilliseconds { get; set; }
    public long UnexplainedScheduledMilliseconds { get; set; }
    public long CoverageTruncationResidualMilliseconds { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public string SourcesJson { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
}
