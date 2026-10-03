namespace SIAMIS.Domain.Entities.Leave;

public enum LeaveEvidenceAction { Recorded, Accepted, Rejected, Superseded }
// Reserved/Exempted are retained only for original D8D historical records; new cases use explicit review.
public enum LeaveSandwichState { Reserved, Charged, Exempted, Released, ReviewPending, ReasonAccepted, ReasonNotAccepted }

/// <summary>Immutable external receipt. No binary or storage identity is accepted in D8D.</summary>
public sealed class EmployeeLeaveEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid LeaveId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public string EvidenceKind { get; set; } = "ExternalReceipt";
    public string ExternalReference { get; set; } = string.Empty;
    public Guid? SupersedesEvidenceId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public sealed class EmployeeLeaveEvidenceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EvidenceId { get; set; }
    public LeaveEvidenceAction Action { get; set; }
    public string? Remarks { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid? ActorId { get; set; }
}

public sealed class EmployeeLeaveApprovalEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid LeaveId { get; set; }
    public Guid EvidenceId { get; set; }
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
}

public sealed class EmployeeLeaveSandwichCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public Guid BeforeLeaveId { get; set; }
    public Guid AfterLeaveId { get; set; }
    public DateOnly GapStart { get; set; }
    public DateOnly GapEnd { get; set; }
    public int Revision { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPaid { get; set; }
    public bool BalanceTracked { get; set; }
    public LeaveSandwichState State { get; set; } = LeaveSandwichState.ReviewPending;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string CalculationSnapshotJson { get; set; } = string.Empty;
}

public sealed class EmployeeLeaveSandwichEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public LeaveSandwichState State { get; set; }
    public Guid? CausingLeaveId { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid? ActorId { get; set; }
}

public sealed class EmployeeLeaveSandwichDate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public DateOnly Date { get; set; }
    public int ScheduledMinutes { get; set; }
    public int SandwichDebitMinutes { get; set; }
}

public sealed class EmployeeLeaveSandwichAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public int LeaveYear { get; set; }
    public int SandwichDebitMinutes { get; set; }
    /// <summary>Frozen entitlement amount selected by HR review; null before review or for legacy history.</summary>
    public int? AppliedDebitMinutes { get; set; }
}
