using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Leave;

[JsonConverter(typeof(JsonStringEnumConverter<LeaveNoticeCategory>))]
public enum LeaveNoticeCategory { Foreseeable, SuddenIllness }
// Future request boundaries describe a continuous range; resolved work intervals are snapshotted by D8C.
public sealed record LeaveRequestedRange(DateOnly StartDate, DateOnly EndDate, TimeOnly? StartTime, TimeOnly? EndTime);
public sealed record WorkIntervalDto(TimeOnly StartTime, TimeOnly EndTime);
public sealed record WeeklyIntervalDto(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public sealed record WorkCalendarDto(Guid Id, string Code, string Name, string? Description, bool IsActive, bool IsDefault, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record CalendarOverrideDto(Guid Id, Guid WorkCalendarId, DateOnly Date, string OverrideType, string? Name, string? Description, IReadOnlyList<WorkIntervalDto> Intervals);
public sealed record CalendarAssignmentDto(Guid Id, Guid EmployeeId, Guid WorkCalendarId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record CalendarResolutionDto(Guid AssignmentId, Guid WorkCalendarId, DateOnly Date, string? OverrideType, IReadOnlyList<WorkIntervalDto> Intervals, int ScheduledMinutes);
public sealed record LeavePolicyDto(Guid Id, Guid LeaveTypeId, string Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Status, DateTime? PublishedAt,
    bool BalanceTracked, int? ForeseeableNoticeHours, bool AllowsSuddenRequest, string SupportingDocumentPolicy, Guid? DocumentTypeId,
    int? CertificateAfterConsecutiveDays, bool CertificateOnMondayWorkingDate, bool CertificateOnFridayWorkingDate, bool SandwichParticipation);
public sealed record EntitlementDto(Guid Id, Guid EmployeeId, Guid LeaveTypeId, int LeaveYear, int EntitledMinutes, long AdjustmentMinutes, long AdjustedEntitledMinutes);
public sealed record EntitlementAdjustmentDto(Guid Id, int AdjustmentMinutes, string Reason, DateTime CreatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkCalendarRequest
{
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WeeklyIntervalRequest
{
    [Required] public DayOfWeek? DayOfWeek { get; set; }
    [Required] public TimeOnly? StartTime { get; set; }
    [Required] public TimeOnly? EndTime { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkIntervalRequest
{
    [Required] public TimeOnly? StartTime { get; set; }
    [Required] public TimeOnly? EndTime { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CalendarOverrideRequest
{
    [Required] public DateOnly? Date { get; set; }
    [Required] public string OverrideType { get; set; } = string.Empty;
    [StringLength(150)] public string? Name { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public List<WorkIntervalRequest> Intervals { get; set; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CalendarAssignmentRequest
{
    [Required] public Guid? WorkCalendarId { get; set; }
    [Required] public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LeavePolicyRequest
{
    [Required] public Guid? LeaveTypeId { get; set; }
    [Required, StringLength(50)] public string Version { get; set; } = string.Empty;
    [Required] public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool BalanceTracked { get; set; }
    [Range(0, int.MaxValue)] public int? ForeseeableNoticeHours { get; set; }
    public bool AllowsSuddenRequest { get; set; }
    public string SupportingDocumentPolicy { get; set; } = "None";
    public Guid? DocumentTypeId { get; set; }
    [Range(0, int.MaxValue)] public int? CertificateAfterConsecutiveDays { get; set; }
    public bool CertificateOnMondayWorkingDate { get; set; }
    public bool CertificateOnFridayWorkingDate { get; set; }
    public bool SandwichParticipation { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntitlementRequest
{
    [Required] public Guid? LeaveTypeId { get; set; }
    [Range(1, 9999)] public int LeaveYear { get; set; }
    [Range(0, int.MaxValue)] public int EntitledMinutes { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EntitlementAdjustmentRequest
{
    public int AdjustmentMinutes { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public interface ILeaveFoundationService
{
    Task<IReadOnlyList<WorkCalendarDto>> CalendarsAsync(CancellationToken ct);
    Task<ServiceResult<WorkCalendarDto>> SaveCalendarAsync(Guid? id, WorkCalendarRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<WeeklyIntervalDto>>> WeeklyAsync(Guid calendarId, CancellationToken ct);
    Task<ServiceResult<WeeklyIntervalDto>> AddWeeklyAsync(Guid calendarId, WeeklyIntervalRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<CalendarOverrideDto>>> OverridesAsync(Guid calendarId, CancellationToken ct);
    Task<ServiceResult<CalendarOverrideDto>> AddOverrideAsync(Guid calendarId, CalendarOverrideRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<CalendarAssignmentDto>>> AssignmentsAsync(Guid employeeId, CancellationToken ct);
    Task<ServiceResult<CalendarAssignmentDto>> AssignAsync(Guid employeeId, CalendarAssignmentRequest request, CancellationToken ct);
    Task<ServiceResult<CalendarResolutionDto>> ResolveCalendarAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<LeavePolicyDto>> PoliciesAsync(Guid? leaveTypeId, CancellationToken ct);
    Task<ServiceResult<LeavePolicyDto>> SavePolicyAsync(Guid? id, LeavePolicyRequest request, CancellationToken ct);
    Task<ServiceResult<LeavePolicyDto>> PublishPolicyAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<LeavePolicyDto>> ResolvePolicyAsync(Guid leaveTypeId, DateOnly date, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<EntitlementDto>>> EntitlementsAsync(Guid employeeId, CancellationToken ct);
    Task<ServiceResult<EntitlementDto>> AddEntitlementAsync(Guid employeeId, EntitlementRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<EntitlementAdjustmentDto>>> AdjustmentsAsync(Guid employeeId, Guid entitlementId, CancellationToken ct);
    Task<ServiceResult<EntitlementDto>> AdjustAsync(Guid employeeId, Guid entitlementId, EntitlementAdjustmentRequest request, CancellationToken ct);
}
