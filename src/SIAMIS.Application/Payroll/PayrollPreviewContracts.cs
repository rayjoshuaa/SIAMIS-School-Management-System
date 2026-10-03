using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed class PayrollPreviewRequest
{
    public IReadOnlyList<Guid>? EmployeeIds { get; set; }
}

public sealed record PayrollPreviewLineDto(
    Guid PayrollComponentId,
    string ComponentCode,
    string ComponentName,
    string ComponentType,
    string CalculationMethod,
    string? PercentageBase,
    decimal? Quantity,
    decimal? Rate,
    decimal Amount,
    string? Remarks,
    Guid? PayrollRuleId = null,
    string? RuleCode = null,
    string? RuleName = null,
    string? ApplicationMode = null,
    string? BaseType = null,
    decimal? BaseAmount = null,
    decimal? MinimumBase = null,
    decimal? MaximumBase = null,
    string SourceType = "Manual",
    Guid? SourceId = null,
    bool IsTaxable = false,
    bool IsStatutory = false,
    string? ContributionSide = null,
    BasicSalaryCalculationSnapshot? BasicSalaryCalculationSnapshot = null,
    string SsoWageTreatmentSnapshot = "Unknown",
    string PitIncomeTreatmentSnapshot = "Unknown",
    string PitPaymentTreatmentSnapshot = "Unknown");

public sealed record PayrollPreviewEmployeeResult(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    string Status,
    decimal? BasicSalary,
    decimal? GrossPay,
    decimal? TotalDeductions,
    decimal? NetPay,
    decimal? TaxableEarnings,
    string Message,
    IReadOnlyList<PayrollPreviewLineDto> Lines,
    EmployeePayrollStatutoryResultDto? SocialSecurity = null);

public sealed record PayrollPreviewSummary(
    Guid PayrollPeriodId,
    int ProcessedEmployees,
    int CalculatedEmployees,
    int SkippedEmployees,
    int FailedEmployees,
    IReadOnlyList<PayrollPreviewEmployeeResult> Results);

public interface IPayrollPreviewService
{
    Task<ServiceResult<PayrollPreviewSummary>> PreviewAsync(Guid payrollPeriodId, PayrollPreviewRequest request, CancellationToken cancellationToken);
}
