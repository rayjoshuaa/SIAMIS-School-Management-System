using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class EmployeeLeaveDto
{
    public Guid LeaveId { get; init; }
    public Guid EmployeeId { get; init; }
    public Guid LeaveTypeId { get; init; }
    public string? LeaveTypeCode { get; init; }
    public string LeaveTypeName { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public int Days { get; init; }
    public string? Reason { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Remarks { get; init; }
}

public sealed class EmployeeLeaveRequest
{
    [Required] public Guid? LeaveTypeId { get; set; }
    [Required] public DateOnly? StartDate { get; set; }
    [Required] public DateOnly? EndDate { get; set; }
    // Accepted for request compatibility but deliberately ignored; Days is calculated by the API.
    public int? Days { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    [StringLength(20)] public string? Status { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

public interface IEmployeeLeaveService
{
    Task<ServiceResult<IReadOnlyList<EmployeeLeaveDto>>> GetLeavesAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> GetLeaveAsync(Guid employeeId, Guid leaveId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> CreateLeaveAsync(Guid employeeId, EmployeeLeaveRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeLeaveDto>> UpdateLeaveAsync(Guid employeeId, Guid leaveId, EmployeeLeaveRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteLeaveAsync(Guid employeeId, Guid leaveId, CancellationToken cancellationToken);
}
