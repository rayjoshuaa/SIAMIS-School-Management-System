using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed class EmployeeContractDto
{
    public Guid EmployeeContractId { get; init; }
    public Guid EmployeeId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public Guid? ContractTypeId { get; init; }
    public string? ContractType { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public DateOnly? ProbationEndDate { get; init; }
    public string ContractStatus { get; init; } = string.Empty;
    public Guid? DocumentId { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class EmployeeContractRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string ContractNumber { get; set; } = string.Empty;
    [Required] public Guid? ContractTypeId { get; set; }
    [Required] public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    [Required, StringLength(40, MinimumLength = 1)] public string ContractStatus { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public interface IEmployeeContractDocumentService
{
    Task<ServiceResult<IReadOnlyList<EmployeeContractDto>>> GetContractsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContractDto>> GetContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContractDto>> CreateContractAsync(Guid employeeId, EmployeeContractRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContractDto>> UpdateContractAsync(Guid employeeId, Guid contractId, EmployeeContractRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken);
}
