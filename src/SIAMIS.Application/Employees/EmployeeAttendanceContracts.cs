using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class EmployeeAttendanceDto
{
    public Guid AttendanceId { get; init; }
    public Guid EmployeeId { get; init; }
    public DateOnly AttendanceDate { get; init; }
    public Guid AttendanceStatusId { get; init; }
    public string? AttendanceStatusCode { get; init; }
    public string AttendanceStatusName { get; init; } = string.Empty;
    public TimeOnly? CheckIn { get; init; }
    public TimeOnly? CheckOut { get; init; }
    public string? Remarks { get; init; }
}

public sealed class EmployeeAttendanceRequest
{
    [Required] public DateOnly? AttendanceDate { get; set; }
    [Required] public Guid? AttendanceStatusId { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

public interface IEmployeeAttendanceService
{
    Task<ServiceResult<IReadOnlyList<EmployeeAttendanceDto>>> GetAttendanceAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeAttendanceDto>> GetAttendanceRecordAsync(Guid employeeId, Guid attendanceId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeAttendanceDto>> CreateAttendanceAsync(Guid employeeId, EmployeeAttendanceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeAttendanceDto>> UpdateAttendanceAsync(Guid employeeId, Guid attendanceId, EmployeeAttendanceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteAttendanceAsync(Guid employeeId, Guid attendanceId, CancellationToken cancellationToken);
}
