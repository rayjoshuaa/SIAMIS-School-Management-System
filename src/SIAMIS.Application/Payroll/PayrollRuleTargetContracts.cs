using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed class PayrollRuleTargetRequest
{
    [Required, StringLength(30)] public string? TargetType { get; set; }
    [Required] public Guid? TargetId { get; set; }
    [Required] public bool? IsExcluded { get; set; }
}

public sealed record PayrollRuleTargetDto(
    Guid PayrollRuleTargetId,
    Guid PayrollRuleId,
    string TargetType,
    Guid TargetId,
    string? TargetName,
    string? TargetCode,
    bool IsExcluded,
    DateTime CreatedAt);

public interface IPayrollRuleTargetService
{
    Task<ServiceResult<IReadOnlyList<PayrollRuleTargetDto>>> GetTargetsAsync(Guid payrollRuleId, CancellationToken cancellationToken);
    Task<PayrollRuleTargetDto?> GetTargetAsync(Guid payrollRuleId, Guid payrollRuleTargetId, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollRuleTargetDto>> CreateTargetAsync(Guid payrollRuleId, PayrollRuleTargetRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollRuleTargetDto>> UpdateTargetAsync(Guid payrollRuleId, Guid payrollRuleTargetId, PayrollRuleTargetRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteTargetAsync(Guid payrollRuleId, Guid payrollRuleTargetId, CancellationToken cancellationToken);
}
