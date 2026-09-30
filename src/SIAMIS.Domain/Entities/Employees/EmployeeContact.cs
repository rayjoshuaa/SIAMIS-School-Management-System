namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeContact
{
    public Guid EmployeeContactId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string? WorkEmail { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? WorkPhone { get; set; }
    public bool IsPrimary { get; set; }
    public Employee Employee { get; set; } = null!;
}
