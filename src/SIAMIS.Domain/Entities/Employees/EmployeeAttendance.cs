using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeAttendance
{
    public Guid AttendanceId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public Guid AttendanceStatusId { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public string? Remarks { get; set; }

    public Employee Employee { get; set; } = null!;
    public AttendanceStatus AttendanceStatus { get; set; } = null!;
}
