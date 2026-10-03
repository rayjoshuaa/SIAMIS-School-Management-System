using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities.Payroll;

/// <summary>Presentation snapshot; source identities inside JSON are historical data, not foreign keys.</summary>
public sealed class EmployeePayslip : IHasTimestamps
{
    public Guid EmployeePayslipId { get; set; } = Guid.NewGuid();
    public Guid EmployeePayrollId { get; set; }
    public int SnapshotVersion { get; set; } = 1;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
