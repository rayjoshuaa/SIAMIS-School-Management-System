namespace SIAMIS.Domain.Entities.Employees;

// Permanent identity snapshot: deliberately no Employee foreign key or cascade deletion.
public sealed class EmployeeNumberReservation
{
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public DateTime ReservedAtUtc { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
}
