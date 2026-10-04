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

    private IQueryable<EmployeeContractDto> ContractQuery() => db.EmployeeContracts.AsNoTracking().Select(x => new EmployeeContractDto
    {
        EmployeeContractId = x.EmployeeContractId, EmployeeId = x.EmployeeId, ContractNumber = x.ContractNumber,
        ContractTypeId = x.ContractTypeId, ContractType = x.ContractType == null ? null : x.ContractType.Name,
        StartDate = x.StartDate, EndDate = x.EndDate, ProbationEndDate = x.ProbationEndDate,
        ContractStatus = x.ContractStatus, DocumentId = x.DocumentId, Notes = x.Notes, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt
    });

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct);
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
}
