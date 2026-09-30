using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class PayrollSettings : IHasTimestamps
{
    public Guid PayrollSettingsId { get; set; } = Guid.NewGuid();
    public string Currency { get; set; } = string.Empty;
    public string PayFrequency { get; set; } = string.Empty;
    public int PayrollCutoffDay { get; set; }
    public int DefaultPayDay { get; set; }
    public decimal WorkingDaysPerPeriod { get; set; }
    public decimal WorkingHoursPerDay { get; set; }
    public string RoundingMode { get; set; } = string.Empty;
    public string BasicSalaryProrationMethod { get; set; } = "ThirtyDay";
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
