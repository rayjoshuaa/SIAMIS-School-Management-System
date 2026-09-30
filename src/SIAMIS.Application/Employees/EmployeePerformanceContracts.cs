using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class EmployeePerformanceDto
{
    public Guid PerformanceRecordId { get; init; }
    public Guid EmployeeId { get; init; }
    public DateOnly ReviewDate { get; init; }
    public DateOnly? ReviewPeriodStart { get; init; }
    public DateOnly? ReviewPeriodEnd { get; init; }
    public Guid PerformanceRatingId { get; init; }
    public string? PerformanceRatingCode { get; init; }
    public string PerformanceRatingName { get; init; } = string.Empty;
    public Guid? ReviewerEmployeeId { get; init; }
    public string? ReviewerEmployeeName { get; init; }
    public string? Strengths { get; init; }
    public string? AreasForImprovement { get; init; }
    public string? Goals { get; init; }
    public string? Remarks { get; init; }
}

public sealed class EmployeePerformanceRequest
{
    [Required] public DateOnly? ReviewDate { get; set; }
    public DateOnly? ReviewPeriodStart { get; set; }
    public DateOnly? ReviewPeriodEnd { get; set; }
    [Required] public Guid? PerformanceRatingId { get; set; }
    public Guid? ReviewerEmployeeId { get; set; }
    [StringLength(4000)] public string? Strengths { get; set; }
    [StringLength(4000)] public string? AreasForImprovement { get; set; }
    [StringLength(4000)] public string? Goals { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

public interface IEmployeePerformanceService
{
    Task<ServiceResult<IReadOnlyList<EmployeePerformanceDto>>> GetPerformanceRecordsAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeePerformanceDto>> GetPerformanceRecordAsync(Guid employeeId, Guid performanceRecordId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeePerformanceDto>> CreatePerformanceRecordAsync(Guid employeeId, EmployeePerformanceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeePerformanceDto>> UpdatePerformanceRecordAsync(Guid employeeId, Guid performanceRecordId, EmployeePerformanceRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeletePerformanceRecordAsync(Guid employeeId, Guid performanceRecordId, CancellationToken cancellationToken);
}
