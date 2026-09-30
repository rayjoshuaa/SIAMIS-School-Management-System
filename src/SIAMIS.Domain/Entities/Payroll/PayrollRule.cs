using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class PayrollRule : IHasTimestamps
{
    public Guid PayrollRuleId { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid PayrollComponentId { get; set; }
    public string ApplicationMode { get; set; } = "Supplement";
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string CalculationMethod { get; set; } = string.Empty;
    public string CalculationStage { get; set; } = "Earning";
    public decimal? Rate { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal? MinimumBase { get; set; }
    public decimal? MaximumBase { get; set; }
    public string? BaseType { get; set; }
    public string AppliesTo { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public PayrollComponent PayrollComponent { get; set; } = null!;
    public ICollection<PayrollRuleTarget> Targets { get; set; } = new List<PayrollRuleTarget>();
}
