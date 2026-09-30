namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeHistory
{
    public Guid EmployeeHistoryId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? Description { get; set; }
    public string? ChangedBy { get; set; }
    public Employee Employee { get; set; } = null!;
}
