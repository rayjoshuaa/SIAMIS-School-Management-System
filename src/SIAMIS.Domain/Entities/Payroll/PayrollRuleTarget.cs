namespace SIAMIS.Domain.Entities.Payroll;

public sealed class PayrollRuleTarget
{
    public Guid PayrollRuleTargetId { get; set; } = Guid.NewGuid();
    public Guid PayrollRuleId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public bool IsExcluded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PayrollRule PayrollRule { get; set; } = null!;
}
