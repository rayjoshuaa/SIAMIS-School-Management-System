using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Leave;

[JsonConverter(typeof(LeaveSandwichExpectedStatusConverter))]
public enum LeaveSandwichExpectedStatus { ReviewPending }
public sealed class LeaveSandwichExpectedStatusConverter() : JsonStringEnumConverter<LeaveSandwichExpectedStatus>(allowIntegerValues: false);
[JsonConverter(typeof(LeaveSandwichReviewOutcomeConverter))]
public enum LeaveSandwichReviewOutcome { ReasonAccepted, ReasonNotAccepted }
public sealed class LeaveSandwichReviewOutcomeConverter() : JsonStringEnumConverter<LeaveSandwichReviewOutcome>(allowIntegerValues: false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LeaveEvidenceRequest
{
    [Required] public Guid? DocumentTypeId { get; set; }
    /// <summary>Opaque external review receipt, never a URL, filesystem path or SIAMIS binary upload.</summary>
    [Required, StringLength(200)] public string ExternalReference { get; set; } = string.Empty;
    public Guid? SupersedesEvidenceId { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LeaveEvidenceReviewRequest
{
    [Required, StringLength(2000)] public string ReviewRemarks { get; set; } = string.Empty;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LeaveSandwichReviewRequest
{
    [Required] public LeaveSandwichExpectedStatus? ExpectedStatus { get; set; }
    [Required] public LeaveSandwichReviewOutcome? Outcome { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
}
public sealed record LeaveEvidenceEventDto(Guid Id, string Action, string? Remarks, DateTime OccurredAt);
public sealed record LeaveEvidenceDto(Guid Id, Guid EmployeeId, Guid LeaveId, Guid DocumentTypeId, string EvidenceKind,
    string ExternalReference, Guid? SupersedesEvidenceId, DateTime RecordedAt, string State, IReadOnlyList<LeaveEvidenceEventDto> History);
public sealed record LeaveEvidenceSummary(IReadOnlyList<Guid> RequiredDocumentTypeIds, bool PrerequisitesSatisfied,
    IReadOnlyList<Guid> ApprovedEvidenceIds, IReadOnlyList<LeaveEvidenceDto> Receipts);
public sealed record LeaveSandwichDateDto(DateOnly Date, int ScheduledMinutes, int SandwichDebitMinutes);
public sealed record LeaveSandwichAllocationDto(int LeaveYear, int SandwichDebitMinutes, int? AppliedDebitMinutes = null)
{
    public int PotentialDebitMinutes => SandwichDebitMinutes;
    public int? UnabsorbedDebitMinutes => AppliedDebitMinutes.HasValue ? SandwichDebitMinutes - AppliedDebitMinutes.Value : null;
}
public sealed record LeaveSandwichEventDto(Guid Id, string State, Guid? CausingLeaveId, string? Reason, DateTime OccurredAt);
public sealed record LeaveSandwichSnapshot(int Version, Guid EmployeeId, Guid LeaveTypeId, Guid BeforeLeaveId, Guid AfterLeaveId,
    [property: JsonRequired] bool IsPaid, [property: JsonRequired] bool BalanceTracked, DateTime ObservedAt, LeaveDateCalculation Before, LeaveDateCalculation After,
    IReadOnlyList<LeaveDateCalculation> GapDates, IReadOnlyList<LeaveSandwichDateDto> Debits, IReadOnlyList<LeaveSandwichAllocationDto> Allocations);
public sealed record LeaveSandwichDto(Guid Id, Guid EmployeeId, Guid LeaveTypeId, Guid BeforeLeaveId, Guid AfterLeaveId,
    int Revision, string State, bool IsPaid, bool BalanceTracked, DateTime DetectedAt, long SandwichDebitMinutes,
    IReadOnlyList<LeaveSandwichDateDto> Dates, IReadOnlyList<LeaveSandwichAllocationDto> Allocations,
    IReadOnlyList<LeaveSandwichEventDto> History)
{
    /// <summary>Configured potential debit is retained separately from the currently applicable policy debit.</summary>
    public long AppliedSandwichDebitMinutes { get; init; }
    public long PotentialDebitMinutes => SandwichDebitMinutes;
    public long AppliedDebitMinutes => AppliedSandwichDebitMinutes;
    public long UnabsorbedDebitMinutes => PotentialDebitMinutes - AppliedDebitMinutes;
    public long CommittedDebitMinutes { get; init; }
    public string? ReviewOutcome { get; init; }
    public DateTime? ReviewedAt { get; init; }
}
public interface ILeaveEvidenceSandwichService
{
    Task<ServiceResult<LeaveEvidenceSummary>> EvidenceAsync(Guid employeeId, Guid leaveId, CancellationToken ct);
    Task<ServiceResult<LeaveEvidenceDto>> RecordEvidenceAsync(Guid employeeId, Guid leaveId, LeaveEvidenceRequest request, CancellationToken ct);
    Task<ServiceResult<LeaveEvidenceDto>> ReviewEvidenceAsync(Guid employeeId, Guid leaveId, Guid evidenceId, bool accept, LeaveEvidenceReviewRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<LeaveSandwichDto>>> SandwichesAsync(Guid employeeId, Guid? leaveId, CancellationToken ct);
    Task<ServiceResult<LeaveSandwichDto>> ReviewSandwichAsync(Guid employeeId, Guid caseId, LeaveSandwichReviewRequest request, CancellationToken ct);
}
