using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeLeave
{
    public Guid LeaveId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    // Optional boundary times on StartDate/EndDate, reserved for D8C's continuous request range.
    // Null for legacy/full-day requests. Resolved per-date working intervals belong in the snapshot.
    public TimeOnly? RequestedStartTime { get; set; }
    public TimeOnly? RequestedEndTime { get; set; }
    public int? ChargeableMinutes { get; set; }
    public int? CalculationSnapshotVersion { get; set; }
    public string? CalculationSnapshotJson { get; set; }
    public int Days { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Remarks { get; set; }

    public Employee Employee { get; set; } = null!;
    public LeaveType LeaveType { get; set; } = null!;
}
