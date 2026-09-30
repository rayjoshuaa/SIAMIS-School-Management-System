using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class PayrollPeriod : IHasTimestamps
{
    public Guid PayrollPeriodId { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly PayDate { get; set; }
    public string Status { get; set; } = "Open";
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<EmployeePayroll> EmployeePayrolls { get; set; } = new List<EmployeePayroll>();
}
