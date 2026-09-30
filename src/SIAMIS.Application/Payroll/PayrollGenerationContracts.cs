using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed class PayrollGenerationRequest
{
    public IReadOnlyList<Guid>? EmployeeIds { get; set; }
    public bool ForceRegenerate { get; set; }
}

public sealed record PayrollGenerationEmployeeResult(
    Guid EmployeeId,
    string EmployeeNumber,
    string Status,
    Guid? PayrollId,
    decimal? GrossPay,
    decimal? TotalDeductions,
    decimal? NetPay,
    string Message);

public sealed record PayrollGenerationSummary(
    Guid PayrollPeriodId,
    int ProcessedEmployees,
    int GeneratedEmployees,
    int SkippedEmployees,
    int FailedEmployees,
    IReadOnlyList<PayrollGenerationEmployeeResult> Results);

public interface IPayrollGenerationService
{
    Task<ServiceResult<PayrollGenerationSummary>> GenerateAsync(Guid payrollPeriodId, PayrollGenerationRequest request, CancellationToken cancellationToken);
}
