using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class EmployeePitPaymentSchedule : IHasTimestamps
{
    public Guid EmployeePitPaymentScheduleId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public int TaxYear { get; set; }
    public int RevisionNumber { get; set; }
    public Guid? ReplacesScheduleId { get; set; }
    public string Status { get; set; } = "Draft";
    public string Evidence { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public ICollection<EmployeePitPaymentScheduleEntry> Entries { get; set; } = new List<EmployeePitPaymentScheduleEntry>();
}
public sealed class EmployeePitPaymentScheduleEntry
{
    public Guid EmployeePitPaymentScheduleEntryId { get; set; } = Guid.NewGuid();
    public Guid EmployeePitPaymentScheduleId { get; set; }
    public int PaymentOrdinal { get; set; }
    public DateOnly PlannedPayDate { get; set; }
}
public sealed class EmployeePitPaymentScheduleSelection
{
    public Guid EmployeeId { get; set; }
    public int TaxYear { get; set; }
    public Guid CurrentScheduleId { get; set; }
}
