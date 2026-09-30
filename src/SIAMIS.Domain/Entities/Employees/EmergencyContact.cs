namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmergencyContact
{
    public Guid EmergencyContactId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AlternativePhone { get; set; }
    public string? Address { get; set; }
    public bool IsPrimary { get; set; }
    public Employee Employee { get; set; } = null!;
}
