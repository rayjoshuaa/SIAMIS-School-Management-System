using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class EmployeeListQuery : IValidatableObject
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((long)(Page - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("The requested page offset exceeds the supported range.", [nameof(Page)]);
    }
    [MaxLength(100)] public string? Search { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public Guid? EmploymentStatusId { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class EmployeeListItemDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string LastName { get; init; } = string.Empty;
    public string? PreferredName { get; init; }
    public string? Department { get; init; }
    public string? Designation { get; init; }
    public string? EmploymentStatus { get; init; }
    public bool IsActive { get; init; }
    public DateOnly? HireDate { get; init; }
    public string? ProfilePhoto { get; init; }
}

public sealed class EmployeeDetailDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public string LastName { get; init; } = string.Empty;
    public string? PreferredName { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public string? Nationality { get; init; }
    public string? ProfilePhoto { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<EmployeeContactDto> Contacts { get; init; } = [];
    public IReadOnlyList<EmployeeAddressDto> Addresses { get; init; } = [];
    public IReadOnlyList<EmergencyContactDto> EmergencyContacts { get; init; } = [];
    public EmploymentSummaryDto? CurrentEmployment { get; init; }
    public TeacherProfileDto? TeacherProfile { get; init; }
    public CompensationSummaryDto? CurrentCompensation { get; set; }
}

public sealed class EmployeeContactDto
{
    public Guid EmployeeContactId { get; init; }
    public string? WorkEmail { get; init; }
    public string? PersonalEmail { get; init; }
    public string? Mobile { get; init; }
    public string? Phone { get; init; }
    public string? WorkPhone { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class EmployeeAddressDto
{
    public Guid EmployeeAddressId { get; init; }
    public Guid AddressTypeId { get; init; }
    public string AddressType { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? StateProvince { get; init; }
    public Guid? CountryId { get; init; }
    public string? Country { get; init; }
    public string? PostalCode { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class EmergencyContactDto
{
    public Guid EmergencyContactId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Relationship { get; init; } = string.Empty;
    public string? Mobile { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class EmploymentSummaryDto
{
    public Guid EmploymentRecordId { get; init; }
    public Guid? DepartmentId { get; init; }
    public string? Department { get; init; }
    public Guid? DesignationId { get; init; }
    public string? Designation { get; init; }
    public Guid? LocationId { get; init; }
    public string? Location { get; init; }
    public Guid? EmploymentTypeId { get; init; }
    public string? EmploymentType { get; init; }
    public Guid? EmploymentStatusId { get; init; }
    public string? EmploymentStatus { get; init; }
    public Guid? HiringSourceId { get; init; }
    public string? HiringSource { get; init; }
    public Guid? ReportingToEmployeeId { get; init; }
    public string? ReportingToEmployeeNumber { get; init; }
    public string? ReportingToName { get; init; }
    public DateOnly HireDate { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class TeacherProfileDto
{
    public Guid TeacherProfileId { get; init; }
    public string TeacherCode { get; init; } = string.Empty;
    public string? TeachingLevel { get; init; }
    public string? Specialization { get; init; }
    public decimal? YearsOfExperience { get; init; }
    public string TeachingStatus { get; init; } = string.Empty;
}

public sealed class CompensationSummaryDto
{
    public Guid EmployeeCompensationId { get; init; }
    public Guid? PayTypeId { get; init; }
    public string? PayType { get; init; }
    public decimal BasicSalary { get; init; }
    public string Currency { get; init; } = "THB";
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
}

public class EmployeeWriteRequest
{
    [Required, StringLength(30, MinimumLength = 1)] public string EmployeeNumber { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 1)] public string FirstName { get; set; } = string.Empty;
    [StringLength(100)] public string? MiddleName { get; set; }
    [Required, StringLength(100, MinimumLength = 1)] public string LastName { get; set; } = string.Empty;
    [StringLength(100)] public string? PreferredName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Guid? GenderId { get; set; }
    public Guid? MaritalStatusId { get; set; }
    public Guid? NationalityId { get; set; }
    [StringLength(500)] public string? ProfilePhoto { get; set; }
    [Required] public Guid? DepartmentId { get; set; }
    [Required] public Guid? DesignationId { get; set; }
    public Guid? LocationId { get; set; }
    [Required] public Guid? EmploymentTypeId { get; set; }
    [Required] public Guid? EmploymentStatusId { get; set; }
    public Guid? HiringSourceId { get; set; }
    public Guid? ReportingToEmployeeId { get; set; }
    [Required] public DateOnly? HireDate { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public IReadOnlyList<EmployeeContactRequest>? Contacts { get; set; }
    public IReadOnlyList<EmployeeAddressRequest>? Addresses { get; set; }
    public IReadOnlyList<EmergencyContactRequest>? EmergencyContacts { get; set; }
    public TeacherProfileRequest? TeacherProfile { get; set; }
}

public sealed class CreateEmployeeRequest : EmployeeWriteRequest { }
public sealed class UpdateEmployeeRequest : EmployeeWriteRequest { }

public sealed class EmployeeContactRequest
{
    [EmailAddress, StringLength(254)] public string? WorkEmail { get; set; }
    [EmailAddress, StringLength(254)] public string? PersonalEmail { get; set; }
    [StringLength(30)] public string? Mobile { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(30)] public string? WorkPhone { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class EmployeeAddressRequest
{
    [Required] public Guid? AddressTypeId { get; set; }
    [Required, StringLength(200, MinimumLength = 1)] public string AddressLine1 { get; set; } = string.Empty;
    [StringLength(200)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? StateProvince { get; set; }
    public Guid? CountryId { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class EmergencyContactRequest
{
    [Required, StringLength(200, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(80, MinimumLength = 1)] public string Relationship { get; set; } = string.Empty;
    [StringLength(30)] public string? Mobile { get; set; }
    [Required, StringLength(30)] public string? Phone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class TeacherProfileRequest
{
    [Required, StringLength(30, MinimumLength = 1)] public string TeacherCode { get; set; } = string.Empty;
    [StringLength(100)] public string? TeachingLevel { get; set; }
    [StringLength(200)] public string? Specialization { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal? YearsOfExperience { get; set; }
    [Required, StringLength(40, MinimumLength = 1)] public string TeachingStatus { get; set; } = string.Empty;
}

public sealed class EmployeeStatusRequest
{
    [Required] public bool? IsActive { get; set; }
}

public sealed record ApiFailure(string Code, string Message, IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record ServiceResult<T>(T? Value, ApiFailure? Failure)
{
    public bool IsSuccess => Failure is null;
    public static ServiceResult<T> Success(T value) => new(value, null);
    public static ServiceResult<T> Fail(string code, string message, IReadOnlyDictionary<string, string[]>? errors = null) => new(default, new(code, message, errors));
}

public interface IEmployeeService
{
    Task<PagedResult<EmployeeListItemDto>> GetEmployeesAsync(EmployeeListQuery query, CancellationToken cancellationToken);
    Task<EmployeeDetailDto?> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeDetailDto>> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeDetailDto>> UpdateEmployeeAsync(Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> SetEmployeeStatusAsync(Guid employeeId, bool isActive, CancellationToken cancellationToken);
}
