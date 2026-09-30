using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class CreateEmployeeContactRequest : IValidatableObject
{
    [EmailAddress, StringLength(254)] public string? WorkEmail { get; set; }
    [EmailAddress, StringLength(254)] public string? PersonalEmail { get; set; }
    [StringLength(30)] public string? Mobile { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(30)] public string? WorkPhone { get; set; }
    public bool IsPrimary { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(WorkEmail)
            && string.IsNullOrWhiteSpace(PersonalEmail)
            && string.IsNullOrWhiteSpace(Mobile)
            && string.IsNullOrWhiteSpace(Phone)
            && string.IsNullOrWhiteSpace(WorkPhone))
        {
            yield return new ValidationResult("At least one contact value must be provided.");
        }
    }
}

/// <summary>Fields left null are preserved from the existing contact.</summary>
public sealed class UpdateEmployeeContactRequest
{
    [EmailAddress, StringLength(254)] public string? WorkEmail { get; set; }
    [EmailAddress, StringLength(254)] public string? PersonalEmail { get; set; }
    [StringLength(30)] public string? Mobile { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(30)] public string? WorkPhone { get; set; }
    public bool? IsPrimary { get; set; }
}

public interface IEmployeeContactsService
{
    Task<ServiceResult<IReadOnlyList<EmployeeContactDto>>> GetContactsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContactDto>> CreateContactAsync(Guid employeeId, CreateEmployeeContactRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContactDto>> UpdateContactAsync(Guid employeeId, Guid contactId, UpdateEmployeeContactRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteContactAsync(Guid employeeId, Guid contactId, CancellationToken cancellationToken);
}
