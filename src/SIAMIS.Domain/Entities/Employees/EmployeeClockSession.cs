namespace SIAMIS.Domain.Entities.Employees;

/// <summary>Immutable opening evidence, with a single append-only closure. HR adjudication never rewrites this provenance.</summary>
public sealed class EmployeeClockSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string WorkArrangement { get; set; } = string.Empty;
    public Guid InEventId { get; set; }
    public Guid? OutEventId { get; set; }
}
