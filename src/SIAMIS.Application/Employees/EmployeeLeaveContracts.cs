using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Leave;

namespace SIAMIS.Application.Employees;

[JsonConverter(typeof(JsonStringEnumConverter<LeaveRequestMode>))]
public enum LeaveRequestMode { FullDay, Timed }

[JsonConverter(typeof(LeaveCancellationExpectedStatusConverter))]
public enum LeaveCancellationExpectedStatus { Pending, Approved }
public sealed class LeaveCancellationExpectedStatusConverter() : JsonStringEnumConverter<LeaveCancellationExpectedStatus>(allowIntegerValues: false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeLeaveRequest
{
    [Required] public Guid? LeaveTypeId { get; set; }
    [Required] public DateOnly? StartDate { get; set; }
    [Required] public DateOnly? EndDate { get; set; }
    [Required] public LeaveRequestMode? RequestMode { get; set; }
    [Required] public LeaveNoticeCategory? NoticeCategory { get; set; }
    public TimeOnly? RequestedStartTime { get; set; }
    public TimeOnly? RequestedEndTime { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LeaveReviewRequest
{
    [StringLength(2000)] public string? ReviewRemarks { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LeaveCancellationRequest
{
    [Required] public LeaveCancellationExpectedStatus? ExpectedStatus { get; set; }
    [StringLength(2000)] public string? CancellationRemarks { get; set; }
}
public sealed class LeaveHistoryQuery
{
    public Guid? EmployeeId { get; set; }
    public Guid? LeaveTypeId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public sealed record LeaveAllocationDto(int LeaveYear, int ChargeableMinutes)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PaidMinutes { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? UnpaidMinutes { get; init; }
}
public sealed record ClassifiedLeaveInterval(TimeOnly StartTime, TimeOnly EndTime, bool IsPaid);
public sealed record LeavePolicyEvidence(Guid Id, string Version, bool BalanceTracked, int? ForeseeableNoticeHours, bool AllowsSuddenRequest,
    string SupportingDocumentPolicy, Guid? DocumentTypeId, int? CertificateAfterConsecutiveDays,
    bool CertificateOnMondayWorkingDate, bool CertificateOnFridayWorkingDate, bool SandwichParticipation, int? SandwichEquivalentDayMinutes = null);
public sealed record ScheduledLeaveInterval(Guid Id, TimeOnly StartTime, TimeOnly EndTime);
public sealed record LeaveDateCalculation(DateOnly Date, Guid EmploymentRecordId, Guid AssignmentId, Guid WorkCalendarId,
    string WorkCalendarCode, string WorkCalendarName, string ScheduleSource, Guid? OverrideId, LeavePolicyEvidence Policy,
    IReadOnlyList<ScheduledLeaveInterval> ScheduledIntervals, IReadOnlyList<WorkIntervalDto> ChargedIntervals, int ChargeableMinutes)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ClassifiedLeaveInterval>? PaymentIntervals { get; init; }
}
public sealed record LeaveCalculationSnapshot(int Version, Guid EmployeeId, Guid LeaveTypeId, string? LeaveTypeCode, string LeaveTypeName, bool? IsPaid,
    string RequestMode, DateOnly StartDate, DateOnly EndDate, TimeOnly? RequestedStartTime, TimeOnly? RequestedEndTime,
    string NoticeCategory, DateTime RequestedAt, string BusinessTimeZone, DateTime FirstChargeableLocalStart, DateTime FirstChargeableUtcStart,
    int? EffectiveNoticeHours, double ActualNoticeHours, bool NoticeSatisfied, bool SuddenRequestUsed, bool BalanceTracked,
    IReadOnlyList<LeaveDateCalculation> Dates, int ChargeableMinutes, IReadOnlyList<LeaveAllocationDto> Allocations,
    int LongestConsecutiveQualifyingDays, bool SupportingDocumentRequired, IReadOnlyList<string> CertificateRequirementReasons)
{
    public IReadOnlyList<Guid>? RequiredDocumentTypeIds { get; init; }
}
public sealed class EmployeeLeaveDto
{
    public Guid LeaveId { get; init; }
    public Guid EmployeeId { get; init; }
    public Guid LeaveTypeId { get; init; }
    public string? LeaveTypeCode { get; init; }
    public string LeaveTypeName { get; init; } = string.Empty;
    // Original requested LeaveType policy fact; V2 actual coverage is in PaymentIntervals and Paid/UnpaidMinutes.
    // Null means legacy evidence is unavailable.
    public bool? IsPaid { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    // Compatibility fact: count of positively charged working dates, not fractional duration or authoritative minutes.
    public int Days { get; init; }
    public string? Reason { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Remarks { get; init; }
    public string? RequestMode { get; init; }
    public string? NoticeCategory { get; init; }
    public TimeOnly? RequestedStartTime { get; init; }
    public TimeOnly? RequestedEndTime { get; init; }
    public int? ChargeableMinutes { get; init; }
    public int? PaidMinutes { get; init; }
    public int? UnpaidMinutes { get; init; }
    public decimal? ChargeableHours => ChargeableMinutes / 60m;
    public DateTime? RequestedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? ReviewRemarks { get; init; }
    public string? CancellationRemarks { get; init; }
    public bool? SupportingDocumentRequired { get; init; }
    public IReadOnlyList<string> CertificateRequirementReasons { get; init; } = [];
    public IReadOnlyList<LeaveAllocationDto> Allocations { get; init; } = [];
    public LeaveCalculationSnapshot? Calculation { get; init; }
    public LeaveEvidenceSummary? Evidence { get; set; }
    public IReadOnlyList<LeaveSandwichDto> SandwichCases { get; set; } = [];
    public long SandwichDebitMinutes { get; set; }
}
public sealed record LeaveHistoryItemDto(Guid LeaveId, Guid EmployeeId, string EmployeeNumber, string EmployeeName, Guid LeaveTypeId,
    string? LeaveTypeCode, string LeaveTypeName, bool? IsPaid, DateOnly StartDate, DateOnly EndDate, string? RequestMode,
    TimeOnly? RequestedStartTime, TimeOnly? RequestedEndTime, int? ChargeableMinutes, string Status, DateTime? RequestedAt,
    string? NoticeCategory, bool? SupportingDocumentRequired, string? Reason)
{
    public int? PaidMinutes { get; init; }
    public int? UnpaidMinutes { get; init; }
    public LeaveEvidenceSummary? Evidence { get; set; }
    public IReadOnlyList<LeaveSandwichDto> SandwichCases { get; set; } = [];
    public long SandwichDebitMinutes { get; set; }
}
public sealed record LeaveBalanceDto(Guid LeaveTypeId, string? Code, string Name, bool? BalanceTracked, string PolicyCoverage,
    int? EntitledMinutes, long? AdjustmentMinutes, long? AdjustedEntitledMinutes, long PendingMinutes, long UsedMinutes, long? AvailableMinutes, long SandwichPendingMinutes = 0, long SandwichUsedMinutes = 0);
public interface IEmployeeLeaveService
{
    Task<ServiceResult<IReadOnlyList<EmployeeLeaveDto>>> GetLeavesAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> GetLeaveAsync(Guid employeeId, Guid leaveId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> CreateLeaveAsync(Guid employeeId, EmployeeLeaveRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> ApproveAsync(Guid employeeId, Guid leaveId, LeaveReviewRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> RejectAsync(Guid employeeId, Guid leaveId, LeaveReviewRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> CancelAsync(Guid employeeId, Guid leaveId, LeaveCancellationRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PagedResult<LeaveHistoryItemDto>>> HistoryAsync(LeaveHistoryQuery query, bool pendingOnly, CancellationToken cancellationToken);
    Task<ServiceResult<IReadOnlyList<LeaveBalanceDto>>> BalancesAsync(Guid employeeId, int year, CancellationToken cancellationToken);
}
