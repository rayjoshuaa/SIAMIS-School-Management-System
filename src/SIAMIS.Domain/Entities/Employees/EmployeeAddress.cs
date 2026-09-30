using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeAddress
{
    public Guid EmployeeAddressId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid AddressTypeId { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public Guid? CountryId { get; set; }
    public string? PostalCode { get; set; }
    public bool IsPrimary { get; set; }
    public Employee Employee { get; set; } = null!;
    public AddressType AddressType { get; set; } = null!;
    public Country? Country { get; set; }
}
