using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.MasterData;

public sealed record PayrollComponentDto(
    Guid PayrollComponentId,
    string Code,
    string Name,
    string ComponentType,
    string? Description,
    string CalculationMethod,
    string? PercentageBase,
    bool IsActive,
    bool IsTaxable,
    bool IsStatutory,
    string? ContributionSide,
    string SsoWageTreatment,
    string PitIncomeTreatment = "Unknown");

public sealed class PayrollComponentRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(30, MinimumLength = 1)] public string ComponentType { get; set; } = string.Empty;
    [Required, StringLength(30, MinimumLength = 1)] public string? CalculationMethod { get; set; }
    [StringLength(30)] public string? PercentageBase { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsStatutory { get; set; }
    [StringLength(20)] public string? ContributionSide { get; set; }
    /// <summary>Unknown, Included, or Excluded. Omitted/null defaults to Unknown on create and preserves the value on update. No legal treatment is inferred.</summary>
    [StringLength(20), RegularExpression("^(Unknown|Included|Excluded)$")] public string? SsoWageTreatment { get; set; }
    /// <summary>Future legal PIT classification only. Omitted/null creates Unknown or preserves the update value; does not change legacy IsTaxable totals.</summary>
    [StringLength(20), RegularExpression("^(Unknown|Included|Excluded)$")] public string? PitIncomeTreatment { get; set; }
}

public sealed class PayrollComponentStatusRequest
{
    [Required] public bool? IsActive { get; set; }
}

public interface IPayrollComponentService
{
    Task<ServiceResult<IReadOnlyList<PayrollComponentDto>>> GetPayrollComponentsAsync(string? componentType, bool includeInactive, string? search,
        bool? isTaxable, bool? isStatutory, string? contributionSide, CancellationToken cancellationToken);
    Task<PayrollComponentDto?> GetPayrollComponentAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollComponentDto>> CreatePayrollComponentAsync(PayrollComponentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollComponentDto>> UpdatePayrollComponentAsync(Guid id, PayrollComponentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> SetPayrollComponentStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeletePayrollComponentAsync(Guid id, CancellationToken cancellationToken);
}
