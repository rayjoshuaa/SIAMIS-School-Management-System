namespace SIAMIS.Domain.Entities.Employees;

/// <summary>Immutable observation evidence. Pairing, corrections and financial effects are separate contracts.</summary>
public sealed class AttendanceEvent
{
    public Guid AttendanceEventId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string BusinessTimeZone { get; set; } = "Asia/Bangkok";
    public string Direction { get; set; } = "Unknown";
    public string Source { get; set; } = "ManualAuthorized";
    public string? SourceKey { get; set; }
    public string? ExternalEventId { get; set; }
    public Guid? ManualRequestKey { get; set; }
    public string? OriginalSourceTimestamp { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public string? Reason { get; set; }
    public Guid? ActorId { get; set; }
    public bool EmployeeWasInactive { get; set; }
    public string EmploymentReadiness { get; set; } = "Ready";
}
