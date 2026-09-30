using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed record PayrollRuleEvaluationEmployeeDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    bool IsActive,
    PayrollRuleEvaluationEmploymentDto? CurrentEmployment);

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
    IReadOnlyList<PayrollRuleEvaluationTargetDto> Targets);

public sealed record PayrollRuleEvaluationDto(
    PayrollRuleEvaluationEmployeeDto Employee,
    PayrollRuleEvaluationPeriodDto PayrollPeriod,
    IReadOnlyList<ApplicablePayrollRuleDto> ApplicableRules);

public interface IPayrollRuleEvaluator
{
    Task<ServiceResult<PayrollRuleEvaluationDto>> EvaluateApplicableRulesAsync(
        Guid payrollPeriodId, Guid employeeId, CancellationToken cancellationToken);
}
