using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Employees;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AttendanceReviewRequest
{
    [Required, Range(0, long.MaxValue)] public long? ExpectedVersion { get; set; }
    [Required, RegularExpression("^[A-F0-9]{64}$")] public string? ExpectedSourceFingerprint { get; set; }
    [Required, StringLength(2000)] public string? Reason { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AttendanceCorrectionRequest : AttendanceReviewRequest
{
    [Required, StringLength(40)] public string? OccurredAt { get; set; }
    [Required] public AttendanceDirection? Direction { get; set; }
    [Required] public Guid? ManualRequestKey { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AttendanceAdjudicationRequest : AttendanceReviewRequest
{
    [Required] public Guid? AttendanceEventId { get; set; }
    [Required] public bool? Included { get; set; }
}
public sealed record AttendanceReviewActionDto(Guid Id, long Sequence, Guid? ReviewCaseId, string Action, Guid? AttendanceEventId,
    Guid? FinalizedRevisionId, string Reason, DateTime OccurredAtUtc, Guid? ActorUserId, string Origin);
public sealed record AttendanceReviewCaseDto(Guid Id, string State, DateTime OpenedAtUtc, Guid? ActorUserId, AttendanceDayDto OriginalCalculation);
public sealed record AttendanceFinalizedSnapshot(int Version, AttendanceDayDto Calculation, IReadOnlyList<AttendanceEventDto> RawEvents,
    IReadOnlyList<AttendanceReviewActionDto> ReviewHistory, bool IsConfirmedAbsent);
public sealed record FinalizedAttendanceRevisionDto(Guid Id, int Revision, DateTime FinalizedAtUtc, Guid? ActorUserId,
    string SourceFingerprint, AttendanceFinalizedSnapshot Snapshot);
public sealed class AttendanceReviewDto
{
    public Guid EmployeeId { get; init; }
    public DateOnly BusinessDate { get; init; }
    public long Version { get; init; }
    public string SourceFingerprint { get; init; } = string.Empty;
    public AttendanceDayDto RawCalculation { get; init; } = null!;
    public AttendanceDayDto Calculation { get; init; } = null!;
    public AttendanceReviewCaseDto? ReviewCase { get; init; }
    public IReadOnlyList<AttendanceReviewActionDto> History { get; init; } = [];
    public FinalizedAttendanceRevisionDto? LatestHistoricalFinalizedRevision { get; init; }
    public bool IsStale { get; init; }
    public bool RequiresReopen { get; init; }
    public bool IsReopened { get; init; }
    public bool IsCurrentlyValidated { get; init; }
    public bool IsConfirmedAbsent { get; init; }
    public IReadOnlyList<AttendanceDayFinding> ChangedSources { get; init; } = [];
}
public interface IAttendanceReviewService
{
    Task<ServiceResult<AttendanceReviewDto>> ReadAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<FinalizedAttendanceRevisionDto>>> HistoryAsync(Guid employeeId, DateOnly date, CancellationToken ct);
    Task<ServiceResult<AttendanceReviewDto>> MutateAsync(Guid employeeId, DateOnly date, string action, AttendanceReviewRequest request, CancellationToken ct);
}
