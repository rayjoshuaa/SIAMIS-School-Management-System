using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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
    DateTime UpdatedAt,
    DateTime? ProcessingStartedAt,
    DateTime? ClosedAt,
    DateTime? CancelledAt,
    string? CancellationReason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PayrollPeriodRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required] public DateOnly? StartDate { get; set; }
    [Required] public DateOnly? EndDate { get; set; }
    [Required] public DateOnly? PayDate { get; set; }
    [StringLength(1000)] public string? Remarks { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PayrollPeriodCancelRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public interface IPayrollPeriodService
{
    Task<ServiceResult<IReadOnlyList<PayrollPeriodDto>>> GetPayrollPeriodsAsync(string? status, DateOnly? fromDate, DateOnly? toDate, string? search, CancellationToken cancellationToken);
    Task<PayrollPeriodDto?> GetPayrollPeriodAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollPeriodDto>> CreatePayrollPeriodAsync(PayrollPeriodRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollPeriodDto>> UpdatePayrollPeriodAsync(Guid id, PayrollPeriodRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> StartProcessingAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> CloseAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> CancelAsync(Guid id, PayrollPeriodCancelRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeletePayrollPeriodAsync(Guid id, CancellationToken cancellationToken);
}
