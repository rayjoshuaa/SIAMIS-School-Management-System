using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class CreateEmployeeAddressRequest
{
    [Required] public Guid? AddressTypeId { get; set; }
    [Required, StringLength(200, MinimumLength = 1)] public string AddressLine1 { get; set; } = string.Empty;
    [StringLength(200)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? StateProvince { get; set; }
    public Guid? CountryId { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    public bool? IsPrimary { get; set; }
}

/// <summary>Null fields are preserved from the existing address.</summary>
public sealed class UpdateEmployeeAddressRequest
{
    public Guid? AddressTypeId { get; set; }
    [StringLength(200, MinimumLength = 1)] public string? AddressLine1 { get; set; }
    [StringLength(200)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? StateProvince { get; set; }
    public Guid? CountryId { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    public bool? IsPrimary { get; set; }
}

public interface IEmployeeAddressesService
{
    Task<ServiceResult<IReadOnlyList<EmployeeAddressDto>>> GetAddressesAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeAddressDto>> CreateAddressAsync(Guid employeeId, CreateEmployeeAddressRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeAddressDto>> UpdateAddressAsync(Guid employeeId, Guid addressId, UpdateEmployeeAddressRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteAddressAsync(Guid employeeId, Guid addressId, CancellationToken cancellationToken);
}
