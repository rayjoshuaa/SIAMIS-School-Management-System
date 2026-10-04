using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Employees;

[JsonConverter(typeof(JsonStringEnumConverter<AttendanceWorkState>))]
public enum AttendanceWorkState { Scheduled, NotScheduled, ConfigurationRequired }
[JsonConverter(typeof(JsonStringEnumConverter<AttendanceTimingState>))]
public enum AttendanceTimingState { Unknown, NotApplicable, OnTime, Late }
[JsonConverter(typeof(JsonStringEnumConverter<AttendanceRecordState>))]
public enum AttendanceRecordState { Live, UnfinalizedPastDay, Finalized, Stale, Reopened, SnapshotInvalid }
[JsonConverter(typeof(JsonStringEnumConverter<AttendanceLeaveState>))]
public enum AttendanceLeaveState { None, Paid, Unpaid, Mixed }

public class AttendanceReportFilter
{
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public bool? Scheduled { get; set; }
    public bool? Late { get; set; }
    public bool? HasApprovedLeave { get; set; }
    public bool? ConfirmedAbsent { get; set; }
    public bool? RequiresReview { get; set; }
    public bool? IsStale { get; set; }
    public bool? Unfinalized { get; set; }
    public AttendanceRecordState? RecordState { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public sealed class AttendanceQueueQuery : AttendanceReportFilter
{
    [Required] public DateOnly? From { get; set; }
    [Required] public DateOnly? To { get; set; }
}
public sealed class AttendanceRangeQuery
{
    [Required] public DateOnly? From { get; set; }
    [Required] public DateOnly? To { get; set; }
}
public sealed record AttendanceReportEvent(Guid AttendanceEventId, DateTime OccurredAtUtc, string Direction, string Source);
public sealed record AttendanceLeaveWindow(TimeOnly StartTime, TimeOnly EndTime);
public sealed record AttendanceReportLeave(Guid LeaveId, bool IsPaid, IReadOnlyList<AttendanceLeaveWindow> ChargedIntervals)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClassifiedLeaveInterval>? PaymentIntervals { get; init; }
}

/// <summary>Privacy-minimized calculation facts. Null coverage totals mean no authoritative partition. Residual is precision metadata only.</summary>
public sealed class AttendanceReportFacts
{
    public string Readiness { get; init; } = "Ready";
    public string? ScheduleKind { get; init; }
    public bool CoveragePartitionAvailable { get; init; }
    public IReadOnlyList<AttendanceTimeInterval> ScheduledIntervals { get; init; } = [];
    public IReadOnlyList<AttendanceReportEvent> Events { get; init; } = [];
    public IReadOnlyList<AttendanceReportLeave> ApprovedLeaves { get; init; } = [];
    public DateTime? FirstObservedInUtc { get; init; }
    public DateTime? LastObservedOutUtc { get; init; }
    public DateTime? ExpectedArrivalUtc { get; init; }
    public long? RawStartVarianceTicks { get; init; }
    public long? RawStartVarianceMilliseconds { get; init; }
    public bool? IsLate { get; init; }
    /// <summary>D9C provisional calculation finding; never an automatic confirmed/completed absence, including before today's shift.</summary>
    public bool PotentialAbsence { get; init; }
    public bool IsConfirmedAbsent { get; init; }
    public AttendanceLeaveState LeaveState { get; init; }
    public string LeaveExtent { get; init; } = "None";
    public long ObservedPresenceMilliseconds { get; init; }
    public long? ScheduledMilliseconds { get; init; }
    public long? PresenceCoveredScheduledMilliseconds { get; init; }
    public long? ApprovedLeaveCoveredScheduledMilliseconds { get; init; }
    public long? PaidLeaveCoveredMilliseconds { get; init; }
    public long? UnpaidLeaveCoveredMilliseconds { get; init; }
    public long? UnexplainedScheduledMilliseconds { get; init; }
    public long? CoverageTruncationResidualMilliseconds { get; init; }
    public string CalculationContractVersion { get; init; } = string.Empty;
    public string GracePolicy { get; init; } = string.Empty;
    public int ClockInGraceMinutes { get; init; }
}
public sealed class AttendanceReportRow
{
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public Guid? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public Guid? DesignationId { get; init; }
    public string? DesignationName { get; init; }
    public DateOnly BusinessDate { get; init; }
    public AttendanceWorkState WorkState { get; init; }
    public AttendanceTimingState TimingState { get; init; }
    public AttendanceRecordState RecordState { get; init; }
    public string DayRelation { get; init; } = string.Empty;
    public string ScheduleWindow { get; init; } = "Unknown";
    public string ArrivalWindow { get; init; } = "Unknown";
    /// <summary>Always Unknown: D9C does not define an open-interval/current-inside algorithm.</summary>
    public string CurrentPresence { get; init; } = "Unknown";
    public bool RequiresReview { get; init; }
    public bool UnfinalizedHistoricalWorkingDay { get; init; }
    public bool ReadyToFinalize { get; init; }
    public string? ReviewCaseState { get; init; }
    public Guid? LatestHistoricalRevisionId { get; init; }
    public int? LatestHistoricalRevision { get; init; }
    public DateTime? FinalizedAtUtc { get; init; }
    public bool IsStale { get; init; }
    public bool RequiresReopen { get; init; }
    public bool IsReopened { get; init; }
    public bool IsCurrentlyValidated { get; init; }
    public IReadOnlyList<AttendanceDayFinding> Findings { get; init; } = [];
    public IReadOnlyList<AttendanceDayFinding> ChangedSources { get; init; } = [];
    public IReadOnlyList<string> AttentionCategories { get; init; } = [];
    public AttendanceReportFacts Live { get; init; } = null!;
    /// <summary>Only the latest currently validated frozen revision. Null for unfinalized, stale, reopened or invalid snapshots. Never a live fallback.</summary>
    public AttendanceReportFacts? Official { get; init; }
}
public sealed record AttendanceReportCounts(int EffectiveEmployees, int EmployeeDates, int Scheduled, int NotScheduled, int OnTime, int Late,
    int ApprovedLeave, int ConfirmedAbsence, int PotentialAbsence, int RequiresReview, int Finalized, int Stale,
    int ConfigurationRequired, int Reopened, int UnfinalizedPastDays, int ReadyToFinalize);
public sealed record AttendanceOverviewDto(DateOnly BusinessDate, string BusinessTimeZone, DateTime GeneratedAtUtc,
    int PopulationCount, AttendanceReportCounts Counts, PagedResult<AttendanceReportRow> Rows);
public sealed record AttendanceHistoryDto(Guid EmployeeId, DateOnly From, DateOnly To, string BusinessTimeZone, DateTime GeneratedAtUtc,
    int RequestedDates, IReadOnlyList<AttendanceReportRow> Dates);
public sealed record AttendanceAttentionQueueDto(DateOnly From, DateOnly To, string BusinessTimeZone, DateTime GeneratedAtUtc,
    int EffectiveEmployeeDates, AttendanceReportCounts Counts, PagedResult<AttendanceReportRow> Rows);
public sealed class AttendanceSummaryDto
{
    public Guid EmployeeId { get; init; }
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string BusinessTimeZone { get; init; } = "Asia/Bangkok";
    public DateTime GeneratedAtUtc { get; init; }
    public int RequestedDates { get; init; }
    public int EffectiveEmploymentDates { get; init; }
    public bool IsComplete { get; init; }
    public int ScheduledWorkingDays { get; init; }
    public int FinalizedWorkingDays { get; init; }
    public int UnfinalizedWorkingDays { get; init; }
    public int StaleFinalizedDays { get; init; }
    public int ReopenedDays { get; init; }
    public int ConfigurationRequiredDays { get; init; }
    public int RequiresReviewDays { get; init; }
    public int OnTimeDays { get; init; }
    public int LateDays { get; init; }
    public int ConfirmedAbsenceDays { get; init; }
    public int PaidLeaveDates { get; init; }
    public int UnpaidLeaveDates { get; init; }
    public int PartialLeaveDates { get; init; }
    public int NonWorkingActivityDates { get; init; }
    public long ScheduledMilliseconds { get; init; }
    public long PresenceCoveredScheduledMilliseconds { get; init; }
    public long ApprovedLeaveCoveredScheduledMilliseconds { get; init; }
    public long PaidLeaveCoveredMilliseconds { get; init; }
    public long UnpaidLeaveCoveredMilliseconds { get; init; }
    public long UnexplainedScheduledMilliseconds { get; init; }
    public long CoverageTruncationResidualMilliseconds { get; init; }
}
public interface IAttendanceReportingService
{
    Task<ServiceResult<AttendanceOverviewDto>> DailyAsync(DateOnly? date, AttendanceReportFilter filter, CancellationToken ct);
    Task<ServiceResult<AttendanceHistoryDto>> HistoryAsync(Guid employeeId, AttendanceRangeQuery query, CancellationToken ct);
    Task<ServiceResult<AttendanceSummaryDto>> SummaryAsync(Guid employeeId, AttendanceRangeQuery query, CancellationToken ct);
    Task<ServiceResult<AttendanceAttentionQueueDto>> QueueAsync(AttendanceQueueQuery query, CancellationToken ct);
}
