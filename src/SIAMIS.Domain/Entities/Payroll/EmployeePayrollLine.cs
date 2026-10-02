using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class EmployeePayrollLine
{
    public Guid EmployeePayrollLineId { get; set; } = Guid.NewGuid();
    public Guid EmployeePayrollId { get; set; }
    public Guid PayrollComponentId { get; set; }
    public string SourceType { get; set; } = "Manual";
    public Guid? SourceId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public bool IsTaxableSnapshot { get; set; }
    public bool IsStatutorySnapshot { get; set; }
    public string? ContributionSideSnapshot { get; set; }
    public string SsoWageTreatmentSnapshot { get; set; } = "Unknown";
    public string PitIncomeTreatmentSnapshot { get; set; } = "Unknown";
    public decimal Amount { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public string? CalculationMethodSnapshot { get; set; }
    public string? RuleCode { get; set; }
    public string? RuleName { get; set; }
    public string? ApplicationMode { get; set; }
    public string? BaseType { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal? MinimumBase { get; set; }
    public decimal? MaximumBase { get; set; }
    public decimal? CalculationRate { get; set; }
    public string? BasicSalaryCalculationSnapshotJson { get; set; }
    public string? Remarks { get; set; }

    public EmployeePayroll EmployeePayroll { get; set; } = null!;
    public PayrollComponent PayrollComponent { get; set; } = null!;
}
