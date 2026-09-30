using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeLeave
{
    public Guid LeaveId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Remarks { get; set; }

    public Employee Employee { get; set; } = null!;
    public LeaveType LeaveType { get; set; } = null!;
}
