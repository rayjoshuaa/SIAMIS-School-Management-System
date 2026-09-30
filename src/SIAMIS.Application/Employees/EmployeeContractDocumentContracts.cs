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

public sealed class EmployeeDocumentDto
{
    public Guid EmployeeDocumentId { get; init; }
    public Guid EmployeeId { get; init; }
    public Guid? DocumentTypeId { get; init; }
    public string? DocumentType { get; init; }
    public string? DocumentNumber { get; init; }
    public DateOnly? IssueDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
    public string VerificationStatus { get; init; } = "Pending";
    public Guid? VerifiedBy { get; init; }
    public DateTime? VerifiedAt { get; init; }
    public string? Remarks { get; init; }
    public DateTime UploadedAt { get; init; }
}

public sealed class EmployeeDocumentRequest
{
    [Required] public Guid? DocumentTypeId { get; set; }
    [StringLength(100)] public string? DocumentNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    [Required, StringLength(260, MinimumLength = 1)] public string FileName { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 1)] public string StorageKey { get; set; } = string.Empty;
    [StringLength(40, MinimumLength = 1)] public string? VerificationStatus { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

public interface IEmployeeContractDocumentService
{
    Task<ServiceResult<IReadOnlyList<EmployeeContractDto>>> GetContractsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContractDto>> GetContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContractDto>> CreateContractAsync(Guid employeeId, EmployeeContractRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeContractDto>> UpdateContractAsync(Guid employeeId, Guid contractId, EmployeeContractRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken);
    Task<ServiceResult<IReadOnlyList<EmployeeDocumentDto>>> GetDocumentsAsync(Guid employeeId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeDocumentDto>> GetDocumentAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeDocumentDto>> CreateDocumentAsync(Guid employeeId, EmployeeDocumentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<EmployeeDocumentDto>> UpdateDocumentAsync(Guid employeeId, Guid documentId, EmployeeDocumentRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<bool>> DeleteDocumentAsync(Guid employeeId, Guid documentId, CancellationToken cancellationToken);
}
