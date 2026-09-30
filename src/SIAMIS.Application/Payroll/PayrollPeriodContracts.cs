using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed record PayrollPeriodDto(
    Guid PayrollPeriodId,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PayDate,
    string Status,
    string? Remarks,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class PayrollPeriodRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required] public DateOnly? StartDate { get; set; }
    [Required] public DateOnly? EndDate { get; set; }
    [Required] public DateOnly? PayDate { get; set; }
    [StringLength(20)] public string? Status { get; set; }
    [StringLength(1000)] public string? Remarks { get; set; }
}

public sealed class PayrollPeriodStatusRequest
{
    [Required] public string Status { get; set; } = string.Empty;
}

public interface IPayrollPeriodService
{
    Task<ServiceResult<IReadOnlyList<PayrollPeriodDto>>> GetPayrollPeriodsAsync(string? status, DateOnly? fromDate, DateOnly? toDate, string? search, CancellationToken cancellationToken);
    Task<PayrollPeriodDto?> GetPayrollPeriodAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollPeriodDto>> CreatePayrollPeriodAsync(PayrollPeriodRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollPeriodDto>> UpdatePayrollPeriodAsync(Guid id, PayrollPeriodRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> SetPayrollPeriodStatusAsync(Guid id, string status, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeletePayrollPeriodAsync(Guid id, CancellationToken cancellationToken);
}
