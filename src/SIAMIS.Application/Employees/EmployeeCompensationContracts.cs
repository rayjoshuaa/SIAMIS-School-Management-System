using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class EmployeeCompensationDto
{
    public Guid CompensationId { get; init; }
    public Guid EmployeeId { get; init; }
    public Guid? PayTypeId { get; init; }
    public string? PayTypeCode { get; init; }
    public string? PayTypeName { get; init; }
    public decimal BasicSalary { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public bool IsCurrent { get; init; }
    public string? Remarks { get; init; }
}

public sealed class EmployeeCompensationRequest
{
    [Required] public Guid? PayTypeId { get; set; }
    [Required, Range(typeof(decimal), "0", "999999999999999.9999")]
    public decimal? BasicSalary { get; set; }
    [Required, StringLength(3, MinimumLength = 3)] public string Currency { get; set; } = string.Empty;
    [Required] public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    [Required] public bool? IsCurrent { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

public interface IEmployeeCompensationService
{
    Task<ServiceResult<IReadOnlyList<EmployeeCompensationDto>>> GetCompensationsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeCompensationDto>> GetCompensationAsync(Guid employeeId, Guid compensationId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeCompensationDto>> CreateCompensationAsync(Guid employeeId, EmployeeCompensationRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeCompensationDto>> UpdateCompensationAsync(Guid employeeId, Guid compensationId, EmployeeCompensationRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteCompensationAsync(Guid employeeId, Guid compensationId, CancellationToken cancellationToken);
}
