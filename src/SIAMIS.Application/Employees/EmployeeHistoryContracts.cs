using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class EmployeeHistoryDto
{
    public Guid EmployeeHistoryId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public DateTime EventDate { get; init; }
    public string? PreviousValue { get; init; }
    public string? NewValue { get; init; }
    public string? Description { get; init; }
    public string? ChangedBy { get; init; }
}

public sealed class CreateEmployeeHistoryRequest
{
    [Required, StringLength(80, MinimumLength = 1)] public string EventType { get; set; } = string.Empty;
    [Required] public DateTime? EventDate { get; set; }
    [StringLength(4000)] public string? PreviousValue { get; set; }
    [StringLength(4000)] public string? NewValue { get; set; }
    [StringLength(2000)] public string? Description { get; set; }
    [StringLength(100)] public string? ChangedBy { get; set; }
}

public interface IEmployeeHistoryService
{
    Task<ServiceResult<IReadOnlyList<EmployeeHistoryDto>>> GetHistoryAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeHistoryDto>> GetHistoryEventAsync(Guid employeeId, Guid historyId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeHistoryDto>> CreateHistoryEventAsync(Guid employeeId, CreateEmployeeHistoryRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteHistoryEventAsync(Guid employeeId, Guid historyId, CancellationToken cancellationToken);
}
