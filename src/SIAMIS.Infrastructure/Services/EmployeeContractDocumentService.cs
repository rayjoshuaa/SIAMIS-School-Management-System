using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeContractDocumentService(SIAMISDbContext db) : IEmployeeContractDocumentService
{
    public async Task<ServiceResult<IReadOnlyList<EmployeeContractDto>>> GetContractsAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<IReadOnlyList<EmployeeContractDto>>("Employee was not found.");
        var rows = await db.EmployeeContracts.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate).ThenBy(x => x.ContractNumber)
            .Select(x => new EmployeeContractDto
            {
                EmployeeContractId = x.EmployeeContractId, EmployeeId = x.EmployeeId, ContractNumber = x.ContractNumber,
                ContractTypeId = x.ContractTypeId, ContractType = x.ContractType == null ? null : x.ContractType.Name,
                StartDate = x.StartDate, EndDate = x.EndDate, ProbationEndDate = x.ProbationEndDate,
                ContractStatus = x.ContractStatus, DocumentId = x.DocumentId, Notes = x.Notes,
                CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
            }).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeContractDto>>.Success(rows);
    }

    public async Task<ServiceResult<EmployeeContractDto>> GetContractAsync(Guid employeeId, Guid contractId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeContractDto>("Employee was not found.");
        var dto = await ContractQuery().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeContractId == contractId, ct);
        return dto is null ? NotFound<EmployeeContractDto>("Contract was not found for this employee.") : ServiceResult<EmployeeContractDto>.Success(dto);
    }

    public async Task<ServiceResult<EmployeeContractDto>> CreateContractAsync(Guid employeeId, EmployeeContractRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeContractDto>("Employee was not found.");
        var validation = await ValidateContract(employeeId, request, ct);
        if (validation is not null) return Invalid<EmployeeContractDto>(validation);
        var contract = new EmployeeContract
        {
            EmployeeId = employeeId, ContractNumber = request.ContractNumber.Trim(), ContractTypeId = request.ContractTypeId,
            StartDate = request.StartDate!.Value, EndDate = request.EndDate, ProbationEndDate = request.ProbationEndDate,
            ContractStatus = request.ContractStatus.Trim(), DocumentId = request.DocumentId, Notes = Clean(request.Notes)
        };
        db.EmployeeContracts.Add(contract);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return Conflict<EmployeeContractDto>("ContractNumber already exists."); }
        return await GetContractAsync(employeeId, contract.EmployeeContractId, ct);
    }

    public async Task<ServiceResult<EmployeeContractDto>> UpdateContractAsync(Guid employeeId, Guid contractId, EmployeeContractRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeContractDto>("Employee was not found.");
        var contract = await db.EmployeeContracts.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeContractId == contractId, ct);
        if (contract is null) return NotFound<EmployeeContractDto>("Contract was not found for this employee.");
        var validation = await ValidateContract(employeeId, request, ct);
        if (validation is not null) return Invalid<EmployeeContractDto>(validation);
        contract.ContractNumber = request.ContractNumber.Trim(); contract.ContractTypeId = request.ContractTypeId;
        contract.StartDate = request.StartDate!.Value; contract.EndDate = request.EndDate;
        contract.ProbationEndDate = request.ProbationEndDate; contract.ContractStatus = request.ContractStatus.Trim();
        contract.DocumentId = request.DocumentId; contract.Notes = Clean(request.Notes);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return Conflict<EmployeeContractDto>("ContractNumber already exists."); }
        return await GetContractAsync(employeeId, contractId, ct);
    }

    public async Task<ServiceResult<bool>> DeleteContractAsync(Guid employeeId, Guid contractId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var contract = await db.EmployeeContracts.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeContractId == contractId, ct);
        if (contract is null) return NotFound<bool>("Contract was not found for this employee.");
        db.EmployeeContracts.Remove(contract); await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<IReadOnlyList<EmployeeDocumentDto>>> GetDocumentsAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<IReadOnlyList<EmployeeDocumentDto>>("Employee was not found.");
        var rows = await db.EmployeeDocuments.AsNoTracking().Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.UploadedAt).ThenBy(x => x.EmployeeDocumentId)
            .Select(x => new EmployeeDocumentDto
            {
                EmployeeDocumentId = x.EmployeeDocumentId, EmployeeId = x.EmployeeId, DocumentTypeId = x.DocumentTypeId,
                DocumentType = x.DocumentType == null ? null : x.DocumentType.Name, DocumentNumber = x.DocumentNumber,
                IssueDate = x.IssueDate, ExpiryDate = x.ExpiryDate, FileName = x.FileName, StorageKey = x.StorageKey,
                VerificationStatus = x.VerificationStatus, VerifiedBy = x.VerifiedBy, VerifiedAt = x.VerifiedAt,
                Remarks = x.Remarks, UploadedAt = x.UploadedAt
            }).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeDocumentDto>>.Success(rows);
    }

    public async Task<ServiceResult<EmployeeDocumentDto>> GetDocumentAsync(Guid employeeId, Guid documentId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeDocumentDto>("Employee was not found.");
        var dto = await DocumentQuery().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeDocumentId == documentId, ct);
        return dto is null ? NotFound<EmployeeDocumentDto>("Document was not found for this employee.") : ServiceResult<EmployeeDocumentDto>.Success(dto);
    }

    public async Task<ServiceResult<EmployeeDocumentDto>> CreateDocumentAsync(Guid employeeId, EmployeeDocumentRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeDocumentDto>("Employee was not found.");
        var validation = await ValidateDocument(request, ct);
        if (validation is not null) return Invalid<EmployeeDocumentDto>(validation);
        var document = new EmployeeDocument
        {
            EmployeeId = employeeId, DocumentTypeId = request.DocumentTypeId, DocumentNumber = Clean(request.DocumentNumber),
            IssueDate = request.IssueDate, ExpiryDate = request.ExpiryDate, FileName = request.FileName.Trim(),
            StorageKey = request.StorageKey.Trim(), VerificationStatus = string.IsNullOrWhiteSpace(request.VerificationStatus) ? "Pending" : request.VerificationStatus.Trim(),
            VerifiedBy = request.VerifiedBy, VerifiedAt = request.VerifiedAt, Remarks = Clean(request.Remarks)
        };
        db.EmployeeDocuments.Add(document); await db.SaveChangesAsync(ct);
        return await GetDocumentAsync(employeeId, document.EmployeeDocumentId, ct);
    }

    public async Task<ServiceResult<EmployeeDocumentDto>> UpdateDocumentAsync(Guid employeeId, Guid documentId, EmployeeDocumentRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeDocumentDto>("Employee was not found.");
        var document = await db.EmployeeDocuments.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeDocumentId == documentId, ct);
        if (document is null) return NotFound<EmployeeDocumentDto>("Document was not found for this employee.");
        var validation = await ValidateDocument(request, ct);
        if (validation is not null) return Invalid<EmployeeDocumentDto>(validation);
        document.DocumentTypeId = request.DocumentTypeId; document.DocumentNumber = Clean(request.DocumentNumber);
        document.IssueDate = request.IssueDate; document.ExpiryDate = request.ExpiryDate;
        document.FileName = request.FileName.Trim(); document.StorageKey = request.StorageKey.Trim();
        document.VerificationStatus = string.IsNullOrWhiteSpace(request.VerificationStatus) ? "Pending" : request.VerificationStatus.Trim();
        document.VerifiedBy = request.VerifiedBy; document.VerifiedAt = request.VerifiedAt; document.Remarks = Clean(request.Remarks);
        await db.SaveChangesAsync(ct);
        return await GetDocumentAsync(employeeId, documentId, ct);
    }

    public async Task<ServiceResult<bool>> DeleteDocumentAsync(Guid employeeId, Guid documentId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var document = await db.EmployeeDocuments.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeDocumentId == documentId, ct);
        if (document is null) return NotFound<bool>("Document was not found for this employee.");
        if (await db.EmployeeContracts.AnyAsync(x => x.EmployeeId == employeeId && x.DocumentId == documentId, ct))
            return Conflict<bool>("This document is linked to an employee contract. Unlink the contract before deleting it.");
        db.EmployeeDocuments.Remove(document); await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<string?> ValidateContract(Guid employeeId, EmployeeContractRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ContractNumber)) return "ContractNumber is required.";
        if (request.ContractNumber.Trim().Length > 50) return "ContractNumber cannot exceed 50 characters.";
        if (!request.ContractTypeId.HasValue || !await db.ContractTypes.AsNoTracking().AnyAsync(x => x.Id == request.ContractTypeId && x.IsActive, ct))
            return "ContractTypeId must reference an active contract type.";
        if (!request.StartDate.HasValue) return "StartDate is required.";
        if (request.EndDate < request.StartDate) return "EndDate cannot be before StartDate.";
        if (request.ProbationEndDate < request.StartDate) return "ProbationEndDate cannot be before StartDate.";
        if (string.IsNullOrWhiteSpace(request.ContractStatus) || request.ContractStatus.Trim().Length > 40) return "ContractStatus is required and cannot exceed 40 characters.";
        if (request.Notes?.Length > 2000) return "Notes cannot exceed 2000 characters.";
        if (request.DocumentId.HasValue && !await db.EmployeeDocuments.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.EmployeeDocumentId == request.DocumentId, ct))
            return "DocumentId must reference a document belonging to this employee.";
        return null;
    }

    private async Task<string?> ValidateDocument(EmployeeDocumentRequest request, CancellationToken ct)
    {
        if (!request.DocumentTypeId.HasValue) return "DocumentTypeId is required.";
        var type = await db.DocumentTypes.AsNoTracking().Where(x => x.Id == request.DocumentTypeId && x.IsActive)
            .Select(x => new { x.RequiresDocumentNumber, x.RequiresExpiryDate, x.RequiresVerification }).SingleOrDefaultAsync(ct);
        if (type is null) return "DocumentTypeId must reference an active document type.";
        if (type.RequiresDocumentNumber && string.IsNullOrWhiteSpace(request.DocumentNumber)) return "DocumentNumber is required for this document type.";
        if (type.RequiresExpiryDate && !request.ExpiryDate.HasValue) return "ExpiryDate is required for this document type.";
        if (request.ExpiryDate.HasValue && request.IssueDate.HasValue && request.ExpiryDate < request.IssueDate)
            return "ExpiryDate cannot be before IssueDate.";
        if (string.IsNullOrWhiteSpace(request.FileName) || request.FileName.Trim().Length > 260) return "FileName is required and cannot exceed 260 characters.";
        if (string.IsNullOrWhiteSpace(request.StorageKey) || request.StorageKey.Trim().Length > 500) return "StorageKey is required and cannot exceed 500 characters.";
        if (request.DocumentNumber?.Length > 100 || request.Remarks?.Length > 2000) return "DocumentNumber or Remarks exceeds the supported length.";
        var status = string.IsNullOrWhiteSpace(request.VerificationStatus) ? "Pending" : request.VerificationStatus.Trim();
        if (status.Length > 40) return "VerificationStatus cannot exceed 40 characters.";
        if ((request.VerifiedBy.HasValue != request.VerifiedAt.HasValue) ||
            (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) && (request.VerifiedBy.HasValue || request.VerifiedAt.HasValue)))
            return "VerifiedBy and VerifiedAt must be supplied together and cannot be set while VerificationStatus is Pending.";
        if (!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) && !request.VerifiedBy.HasValue)
            return "VerifiedBy and VerifiedAt are required when VerificationStatus is not Pending.";
        if (!type.RequiresVerification && (!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase) || request.VerifiedBy.HasValue))
            return "Verification fields are not enabled for this document type.";
        if (request.VerifiedBy.HasValue && !await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == request.VerifiedBy, ct))
            return "VerifiedBy must reference an existing employee.";
        return null;
    }

    private IQueryable<EmployeeContractDto> ContractQuery() => db.EmployeeContracts.AsNoTracking().Select(x => new EmployeeContractDto
    {
        EmployeeContractId = x.EmployeeContractId, EmployeeId = x.EmployeeId, ContractNumber = x.ContractNumber,
        ContractTypeId = x.ContractTypeId, ContractType = x.ContractType == null ? null : x.ContractType.Name,
        StartDate = x.StartDate, EndDate = x.EndDate, ProbationEndDate = x.ProbationEndDate,
        ContractStatus = x.ContractStatus, DocumentId = x.DocumentId, Notes = x.Notes, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
    });

    private IQueryable<EmployeeDocumentDto> DocumentQuery() => db.EmployeeDocuments.AsNoTracking().Select(x => new EmployeeDocumentDto
    {
        EmployeeDocumentId = x.EmployeeDocumentId, EmployeeId = x.EmployeeId, DocumentTypeId = x.DocumentTypeId,
        DocumentType = x.DocumentType == null ? null : x.DocumentType.Name, DocumentNumber = x.DocumentNumber,
        IssueDate = x.IssueDate, ExpiryDate = x.ExpiryDate, FileName = x.FileName, StorageKey = x.StorageKey,
        VerificationStatus = x.VerificationStatus, VerifiedBy = x.VerifiedBy, VerifiedAt = x.VerifiedAt, Remarks = x.Remarks, UploadedAt = x.UploadedAt
    });

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct);
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
}
