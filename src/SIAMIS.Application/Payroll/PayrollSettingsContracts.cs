using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed class PayrollSettingsRequest
{
    [StringLength(10)] public string? Currency { get; set; }
    [StringLength(30)] public string? PayFrequency { get; set; }
    public int? PayrollCutoffDay { get; set; }
    public int? DefaultPayDay { get; set; }
    public decimal? WorkingDaysPerPeriod { get; set; }
    public decimal? WorkingHoursPerDay { get; set; }
    [StringLength(20)] public string? RoundingMode { get; set; }
    /// <summary>Approved Monthly salary policy. Only ThirtyDay is currently supported.</summary>
    [StringLength(30)] public string? BasicSalaryProrationMethod { get; set; } = "ThirtyDay";
    public int? DecimalPlaces { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class PayrollSettingsStatusRequest
{
    [Required] public bool? IsActive { get; set; }
}

public sealed record PayrollSettingsDto(
    Guid PayrollSettingsId,
    string Currency,
    string PayFrequency,
    int PayrollCutoffDay,
    int DefaultPayDay,
    decimal WorkingDaysPerPeriod,
    decimal WorkingHoursPerDay,
    string RoundingMode,
    int DecimalPlaces,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string BasicSalaryProrationMethod);

public interface IPayrollSettingsService
{
    Task<PayrollSettingsDto?> GetCurrentPayrollSettingsAsync(CancellationToken cancellationToken);
    Task<PayrollSettingsDto?> GetPayrollSettingsAsync(Guid payrollSettingsId, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollSettingsDto>> CreatePayrollSettingsAsync(PayrollSettingsRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<PayrollSettingsDto>> UpdatePayrollSettingsAsync(Guid payrollSettingsId, PayrollSettingsRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> SetPayrollSettingsStatusAsync(Guid payrollSettingsId, bool isActive, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeletePayrollSettingsAsync(Guid payrollSettingsId, CancellationToken cancellationToken);
}
