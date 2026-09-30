using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeContract : IHasTimestamps
{
    public Guid EmployeeContractId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public Guid? ContractTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public string ContractStatus { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Employee Employee { get; set; } = null!;
    public ContractType? ContractType { get; set; }
    public EmployeeDocument? Document { get; set; }
}
