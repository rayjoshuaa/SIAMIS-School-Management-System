using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeCompensation
{
    public Guid EmployeeCompensationId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid? PayTypeId { get; set; }
    public decimal BasicSalary { get; set; }
    public string Currency { get; set; } = "THB";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsCurrent { get; set; }
    public string? Remarks { get; set; }
    public Employee Employee { get; set; } = null!;
    public PayType? PayType { get; set; }
}
