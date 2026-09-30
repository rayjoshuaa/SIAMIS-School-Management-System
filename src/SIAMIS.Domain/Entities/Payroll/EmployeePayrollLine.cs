using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class EmployeePayrollLine
{
    public Guid EmployeePayrollLineId { get; set; } = Guid.NewGuid();
    public Guid EmployeePayrollId { get; set; }
    public Guid PayrollComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public string? Remarks { get; set; }

    public EmployeePayroll EmployeePayroll { get; set; } = null!;
    public PayrollComponent PayrollComponent { get; set; } = null!;
}
