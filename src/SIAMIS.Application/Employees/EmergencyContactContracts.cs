using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class CreateEmergencyContactRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(80, MinimumLength = 1)] public string Relationship { get; set; } = string.Empty;
    [Required, StringLength(30, MinimumLength = 1)] public string Phone { get; set; } = string.Empty;
    [StringLength(30)] public string? AlternativePhone { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    public bool? IsPrimary { get; set; }
}

/// <summary>Null values preserve the corresponding field on update.</summary>
public sealed class UpdateEmergencyContactRequest
{
    [StringLength(200, MinimumLength = 1)] public string? Name { get; set; }
    [StringLength(80, MinimumLength = 1)] public string? Relationship { get; set; }
    [StringLength(30, MinimumLength = 1)] public string? Phone { get; set; }
    [StringLength(30)] public string? AlternativePhone { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    public bool? IsPrimary { get; set; }
}

public sealed class EmployeeEmergencyContactDto
{
    public Guid EmergencyContactId { get; init; }
    public Guid EmployeeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Relationship { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? AlternativePhone { get; init; }
    public string? Address { get; init; }
    public bool IsPrimary { get; init; }
}

public interface IEmployeeEmergencyContactsService
{
    Task<ServiceResult<IReadOnlyList<EmployeeEmergencyContactDto>>> GetEmergencyContactsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeEmergencyContactDto>> CreateEmergencyContactAsync(Guid employeeId, CreateEmergencyContactRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeEmergencyContactDto>> UpdateEmergencyContactAsync(Guid employeeId, Guid emergencyContactId, UpdateEmergencyContactRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteEmergencyContactAsync(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken);
}
