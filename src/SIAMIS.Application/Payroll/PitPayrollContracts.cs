using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Application.Payroll;

/// <summary>Versioned integration evidence wrapping the unchanged D6D monetary result.</summary>
public sealed record PitPayrollSnapshot(string Version, Guid EmployeeStatutoryEnrollmentId,
    PitPaymentScheduleDto Schedule, PitCalculationSnapshot Calculation);
/// <summary>Preview contains no purported persisted PIT result identifier.</summary>
public sealed record PitPayrollPreviewDto(string Status, IReadOnlyList<string> Reasons,
    PitPayrollSnapshot? Snapshot = null);
public sealed record EmployeePayrollPitResultDto(Guid EmployeePayrollPitResultId, Guid EmployeePayrollId,
    Guid EmployeeId, Guid PayrollPeriodId, DateOnly GoverningDate, int TaxYear, string Currency,
    string CalculationMethodVersion, decimal CurrentWithholding, string CalculationSnapshotJson, DateTime CreatedAt);
public sealed record PitPayrollIntegration(PayrollCalculationResult Calculation, EmployeePayrollPitResult? Result,
    PitPayrollPreviewDto Preview);
public interface IPitPayrollService
{
    Task<ServiceResult<PitPayrollIntegration>> CalculateAsync(Guid employeeId, PayrollPeriod period,
        Guid intendedPayrollId, Guid? replacedPayrollId, BasicSalaryCalculationSnapshot salary,
        PayrollCalculationResult calculation, EmployeePayrollStatutoryResult? sso, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollPitResultDto?>> GetResultAsync(Guid payrollId, CancellationToken ct);
}
