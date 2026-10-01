using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed record PayrollRuleEvaluationEmployeeDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    bool IsActive,
    // CurrentEmployment is retained for response compatibility; it now contains period-effective context.
    PayrollRuleEvaluationEmploymentDto? CurrentEmployment,
    DateOnly? TargetContextDate = null,
    Guid? EmploymentRecordId = null);

public sealed record PayrollRuleEvaluationEmploymentDto(
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? DesignationId,
    string? DesignationName,
    Guid? EmploymentTypeId,
    string? EmploymentTypeName,
    Guid? LocationId,
    string? LocationName);

public sealed record PayrollRuleEvaluationPeriodDto(
    Guid PayrollPeriodId,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

public sealed record PayrollRuleEvaluationTargetDto(
    Guid PayrollRuleTargetId,
    string TargetType,
    Guid TargetId,
    string? TargetName,
    string? TargetCode,
    bool IsExcluded,
    bool IsMatched);

public sealed record ApplicablePayrollRuleDto(
    Guid PayrollRuleId,
    string Code,
    string Name,
    Guid PayrollComponentId,
    string? PayrollComponentCode,
    string PayrollComponentName,
    string PayrollComponentType,
    string ApplicationMode,
    string RuleType,
    string CalculationStage,
    int Priority,
    string CalculationMethod,
    string? BaseType,
    decimal? Rate,
    decimal? FixedAmount,
    decimal? MinimumBase,
    decimal? MaximumBase,
    string AppliesTo,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string ApplicabilitySummary,
    IReadOnlyList<PayrollRuleEvaluationTargetDto> Targets,
    bool PayrollComponentIsTaxable = false,
    bool PayrollComponentIsStatutory = false,
    string? PayrollComponentContributionSide = null,
    string PayrollComponentSsoWageTreatment = "Unknown");

public sealed record PayrollRuleEvaluationDto(
    PayrollRuleEvaluationEmployeeDto Employee,
    PayrollRuleEvaluationPeriodDto PayrollPeriod,
    IReadOnlyList<ApplicablePayrollRuleDto> ApplicableRules);

public interface IPayrollRuleEvaluator
{
    Task<ServiceResult<PayrollRuleEvaluationDto>> EvaluateApplicableRulesAsync(
        Guid payrollPeriodId, Guid employeeId, CancellationToken cancellationToken);
    /// <summary>Uses an already-resolved context from the preview batch or generation transaction.</summary>
    Task<ServiceResult<PayrollRuleEvaluationDto>> EvaluateApplicableRulesAsync(
        Guid payrollPeriodId, Guid employeeId, PayrollEmploymentContextDto employmentContext, CancellationToken cancellationToken);
}
