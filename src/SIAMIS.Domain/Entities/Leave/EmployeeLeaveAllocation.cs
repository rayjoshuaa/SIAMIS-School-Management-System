namespace SIAMIS.Domain.Entities.Leave;

/// <summary>Immutable server-generated calendar-year allocation; status on its parent determines Pending/Used.</summary>
public sealed class EmployeeLeaveAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeLeaveId { get; set; }
    public int LeaveYear { get; set; }
    public int ChargeableMinutes { get; set; }
    public int? PaidMinutes { get; set; }
    public int? UnpaidMinutes { get; set; }
}
