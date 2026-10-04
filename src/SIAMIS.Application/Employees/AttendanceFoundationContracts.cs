using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Employees;

[JsonConverter(typeof(AttendanceDirectionConverter))]
public enum AttendanceDirection { In, Out, Unknown }
public sealed class AttendanceDirectionConverter() : JsonStringEnumConverter<AttendanceDirection>(allowIntegerValues: false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ManualAttendanceEventRequest
{
    /// <summary>ISO 8601 instant with explicit Z or numeric offset; up to seven fractional second digits.</summary>
    [Required, StringLength(40)] public string? OccurredAt { get; set; }
    [Required] public AttendanceDirection? Direction { get; set; }
    [Required] public Guid? ManualRequestKey { get; set; }
    [Required, StringLength(2000)] public string? Reason { get; set; }
}

public sealed record AttendanceEventDto(Guid AttendanceEventId, Guid EmployeeId, DateTime OccurredAtUtc,
    DateOnly BusinessDate, string BusinessTimeZone, string Direction, string Source, string? SourceKey,
    string? ExternalEventId, Guid? ManualRequestKey, string? OriginalSourceTimestamp, DateTime ReceivedAtUtc,
    string? Reason, Guid? ActorId, bool EmployeeWasInactive, string EmploymentReadiness)
{
    public IReadOnlyList<string> IntakeAnomalies =>
        (EmployeeWasInactive ? new[] { "EmployeeInactiveAtReceipt" } : Array.Empty<string>())
        .Concat(EmploymentReadiness == "Ready" ? Array.Empty<string>() : new[] { EmploymentReadiness }).ToArray();
}
public sealed record ManualAttendanceEventResult(AttendanceEventDto Event, bool IsReplay);
public sealed record AttendanceEmploymentContext(Guid EmploymentRecordId, DateOnly EmploymentStart, DateOnly? EmploymentEnd,
    Guid? DepartmentId, Guid? DesignationId, Guid? LocationId, Guid? EmploymentTypeId, Guid? EmploymentStatusId);
public sealed record AttendanceScheduleInterval(Guid IntervalId, TimeOnly StartTime, TimeOnly EndTime);
public sealed record AttendanceExpectedWorkDto(Guid EmployeeId, DateOnly Date, string BusinessTimeZone,
    bool EmployeeIsActive, string Readiness, string? Finding, AttendanceEmploymentContext? Employment,
    Guid? AssignmentId, Guid? WorkCalendarId, string? CalendarCode, string? CalendarName,
    Guid? OverrideId, string? ScheduleKind, IReadOnlyList<AttendanceScheduleInterval> Intervals);

public interface IAttendanceFoundationService
{
    Task<ServiceResult<ManualAttendanceEventResult>> CreateManualAsync(Guid employeeId, ManualAttendanceEventRequest request, CancellationToken ct);
    Task<ServiceResult<AttendanceEventDto>> EventAsync(Guid employeeId, Guid eventId, CancellationToken ct);
    Task<ServiceResult<PagedResult<AttendanceEventDto>>> EventsAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken ct);
    Task<ServiceResult<AttendanceExpectedWorkDto>> ExpectedWorkAsync(Guid employeeId, DateOnly date, CancellationToken ct);
}
