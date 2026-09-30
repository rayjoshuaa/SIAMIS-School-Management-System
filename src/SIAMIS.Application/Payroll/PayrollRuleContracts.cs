using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed class PayrollRuleListQuery
{
    [StringLength(30)] public string? RuleType { get; set; }
    [StringLength(30)] public string? CalculationMethod { get; set; }
    [StringLength(30)] public string? AppliesTo { get; set; }
    public bool? IsActive { get; set; }
    public DateOnly? ActiveOn { get; set; }
    [StringLength(200)] public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class PayrollRuleRequest
{
    [StringLength(50)] public string? Code { get; set; }
    [StringLength(150)] public string? Name { get; set; }
    [Required, Range(0, int.MaxValue)] public int? Priority { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(30)] public string? RuleType { get; set; }
    [StringLength(30)] public string? CalculationMethod { get; set; }
    public decimal? Rate { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal? MinimumBase { get; set; }
    public decimal? MaximumBase { get; set; }
    [StringLength(30)] public string? BaseType { get; set; }
    [StringLength(30)] public string? AppliesTo { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class PayrollRuleStatusRequest
{
    [Required] public bool? IsActive { get; set; }
}

public sealed record PayrollRuleDto(
    Guid PayrollRuleId,
    string Code,
    string Name,
    int Priority,
    string? Description,
    string RuleType,
    string CalculationMethod,
    decimal? Rate,
    decimal? FixedAmount,
    decimal? MinimumBase,
    decimal? MaximumBase,
    string? BaseType,
    string AppliesTo,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public interface IPayrollRuleService
{
    Task<ServiceResult<PagedResult<PayrollRuleDto>>> GetPayrollRulesAsync(PayrollRuleListQuery query, CancellationToken cancellationToken);
    Task<PayrollRuleDto?> GetPayrollRuleAsync(Guid payrollRuleId, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollRuleDto>> CreatePayrollRuleAsync(PayrollRuleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollRuleDto>> UpdatePayrollRuleAsync(Guid payrollRuleId, PayrollRuleRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> SetPayrollRuleStatusAsync(Guid payrollRuleId, bool isActive, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeletePayrollRuleAsync(Guid payrollRuleId, CancellationToken cancellationToken);
}
