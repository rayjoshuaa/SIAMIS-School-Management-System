using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class EmployeePayrollComponentAssignment : IHasTimestamps
{
    public Guid EmployeePayrollComponentAssignmentId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid PayrollComponentId { get; set; }
    public decimal Amount { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Employee Employee { get; set; } = null!;
    public PayrollComponent PayrollComponent { get; set; } = null!;
}
