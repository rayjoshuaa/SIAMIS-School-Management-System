using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed class EmployeePayrollListQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public Guid? PayrollPeriodId { get; set; }
    public Guid? EmployeeId { get; set; }
    [StringLength(30)] public string? Status { get; set; }
}

public sealed record EmployeePayrollDto(
    Guid EmployeePayrollId,
    Guid PayrollPeriodId,
    Guid EmployeeId,
    decimal BasicSalary,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    string Status,
    string? Remarks,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record EmployeePayrollListItemDto(
    EmployeePayrollDto Payroll,
    string EmployeeNumber,
    string EmployeeName,
    string PayrollPeriodCode,
    string PayrollPeriodName);

public sealed record EmployeePayrollEmployeeSummaryDto(Guid EmployeeId, string EmployeeNumber, string EmployeeName, bool IsActive);

public sealed record EmployeePayrollPeriodSummaryDto(
    Guid PayrollPeriodId,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PayDate,
    string Status);

public sealed record EmployeePayrollDetailDto(
    EmployeePayrollDto Payroll,
    EmployeePayrollEmployeeSummaryDto Employee,
    EmployeePayrollPeriodSummaryDto PayrollPeriod,
    IReadOnlyList<EmployeePayrollLineDto> Lines);

public sealed class EmployeePayrollRequest
{
    [Required] public Guid? PayrollPeriodId { get; set; }
    [Required] public Guid? EmployeeId { get; set; }
    [Required, Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? BasicSalary { get; set; }
    [Required, Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? GrossPay { get; set; }
    [Required, Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? TotalDeductions { get; set; }
    [Required, Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? NetPay { get; set; }
    [StringLength(30)] public string? Status { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

public sealed class EmployeePayrollStatusRequest
{
    [Required, StringLength(30)] public string Status { get; set; } = string.Empty;
}

public sealed record EmployeePayrollLineDto(
    Guid EmployeePayrollLineId,
    Guid EmployeePayrollId,
    Guid PayrollComponentId,
    string ComponentCode,
    string ComponentName,
    string ComponentType,
    decimal Amount,
    decimal? Quantity,
    decimal? Rate,
    string? Remarks,
    string SourceType,
    Guid? SourceId,
    string? CalculationMethodSnapshot,
    string? RuleCode,
    string? RuleName,
    string? ApplicationMode,
    string? BaseType,
    decimal? BaseAmount,
    decimal? MinimumBase,
    decimal? MaximumBase,
    decimal? CalculationRate);

public sealed class EmployeePayrollLineRequest
{
    [Required] public Guid? PayrollComponentId { get; set; }
    [Required, Range(typeof(decimal), "0.0001", "999999999999999.9999")] public decimal? Amount { get; set; }
    [Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? Quantity { get; set; }
    [Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? Rate { get; set; }
    [Required, StringLength(1000)] public string? Remarks { get; set; }
}

public interface IEmployeePayrollService
{
    Task<ServiceResult<PagedResult<EmployeePayrollListItemDto>>> GetPayrollsAsync(EmployeePayrollListQuery query, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollDetailDto>> GetPayrollAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollDetailDto>> CreatePayrollAsync(EmployeePayrollRequest request, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollDetailDto>> UpdatePayrollAsync(Guid id, EmployeePayrollRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> SetPayrollStatusAsync(Guid id, string status, CancellationToken ct);
    Task<ServiceResult<bool>> DeletePayrollAsync(Guid id, CancellationToken ct);

    Task<ServiceResult<IReadOnlyList<EmployeePayrollLineDto>>> GetLinesAsync(Guid payrollId, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollLineDto>> GetLineAsync(Guid payrollId, Guid lineId, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollLineDto>> CreateLineAsync(Guid payrollId, EmployeePayrollLineRequest request, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollLineDto>> UpdateLineAsync(Guid payrollId, Guid lineId, EmployeePayrollLineRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteLineAsync(Guid payrollId, Guid lineId, CancellationToken ct);
}
