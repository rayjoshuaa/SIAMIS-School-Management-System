using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class EmployeePayroll : IHasTimestamps
{
    public Guid EmployeePayrollId { get; set; } = Guid.NewGuid();
    public Guid PayrollPeriodId { get; set; }
    public Guid EmployeeId { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal GrossPay { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PayrollPeriod PayrollPeriod { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public ICollection<EmployeePayrollLine> Lines { get; set; } = new List<EmployeePayrollLine>();
}
