using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;
using SIAMIS.Infrastructure.Services;

namespace SIAMIS.Infrastructure.Documents;

public sealed class HrDocumentService(SIAMISDbContext db, IPrivateDocumentStorage storage, ICurrentActor actor, ILogger<HrDocumentService> log) : IHrDocumentService
{
    public static readonly IReadOnlySet<string> Categories = new HashSet<string>(StringComparer.Ordinal)
        { "EmploymentContract", "Identification", "WorkAuthorization", "QualificationOrCertificate", "GeneralHRDocument" };
    private bool Allowed(bool write) => actor.UserId.HasValue && actor.HasCapability(write ? "HRDocuments.Manage" : "HRDocuments.Read");
    private bool LeaveAllowed(EmployeeDocument doc) => !doc.LeaveEvidenceId.HasValue || actor.HasCapability("Leave.Evidence");
    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
    private static HrDocumentDto Dto(EmployeeDocument x) => new(x.EmployeeDocumentId,x.EmployeeId,x.DocumentTypeId,x.EmploymentRecordId,x.LeaveId,x.LeaveEvidenceId,
        x.Category,x.FileName,x.ContentType,x.SizeBytes,x.ContentSha256,x.CreatedByUserId,DateTime.SpecifyKind(x.UploadedAt,DateTimeKind.Utc),x.LifecycleStatus,x.Version,x.SupersedesDocumentId,
        x.LifecycleChangedAtUtc.HasValue ? DateTime.SpecifyKind(x.LifecycleChangedAtUtc.Value,DateTimeKind.Utc) : null,x.LifecycleChangedByUserId,x.Remarks,x.DocumentNumber,x.IssueDate,x.ExpiryDate,x.VerificationStatus,x.VerifiedBy,x.VerifiedAt);
    private IQueryable<EmployeeDocument> Owned(Guid id, Guid? employeeId) => db.EmployeeDocuments.Where(x => x.EmployeeDocumentId == id && (!employeeId.HasValue || x.EmployeeId == employeeId));
    private void Audit(EmployeeDocument doc, string operation) => db.Add(new SecurityAuditEvent { ActorUserId=actor.UserId,
        ResourceType="EmployeeDocument",ResourceId=doc.EmployeeDocumentId.ToString(),Operation=$"{operation};EmployeeId={doc.EmployeeId}" });
    public async Task<ServiceResult<IReadOnlyList<HrDocumentDto>>> ListAsync(Guid employeeId, bool includeHistory, string? category, CancellationToken ct)
    {
        if(!Allowed(false)) return Fail<IReadOnlyList<HrDocumentDto>>("forbidden","HR document access is required.");
        if(category is not null && !Categories.Contains(category)) return Fail<IReadOnlyList<HrDocumentDto>>("validation","Unknown document category.");
        if(!await db.Employees.AsNoTracking().AnyAsync(x=>x.EmployeeId==employeeId,ct)) return Fail<IReadOnlyList<HrDocumentDto>>("not_found","Employee was not found.");
        var rows=await db.EmployeeDocuments.AsNoTracking().Where(x=>x.EmployeeId==employeeId && (includeHistory || x.LifecycleStatus=="Active" || x.LifecycleStatus=="MetadataOnly") && (category==null || x.Category==category))
            .OrderByDescending(x=>x.UploadedAt).ThenBy(x=>x.EmployeeDocumentId).ToListAsync(ct);
        if(rows.Any(x=>!LeaveAllowed(x))) return Fail<IReadOnlyList<HrDocumentDto>>("forbidden","Leave evidence access is required.");
        return ServiceResult<IReadOnlyList<HrDocumentDto>>.Success(rows.Select(Dto).ToArray());
    }
    public async Task<ServiceResult<HrDocumentDto>> GetAsync(Guid id, Guid? employeeId, CancellationToken ct)
    {
        if(!Allowed(false)) return Fail<HrDocumentDto>("forbidden","HR document access is required.");
        var doc=await Owned(id,employeeId).AsNoTracking().SingleOrDefaultAsync(ct);
        if(doc is null) return Fail<HrDocumentDto>("not_found","Document was not found.");
        if(!LeaveAllowed(doc)) return Fail<HrDocumentDto>("forbidden","Leave evidence access is required.");
        return ServiceResult<HrDocumentDto>.Success(Dto(doc));
    }
    private async Task<string?> ValidateAsync(Guid employeeId, HrDocumentInput input, CancellationToken ct)
    {
        if(!Categories.Contains(input.Category)) return "Unknown document category.";
        if(input.Remarks?.Length>2000 || input.DocumentNumber?.Length>100) return "Document metadata exceeds the supported length.";
        if(input.ExpiryDate<input.IssueDate) return "ExpiryDate cannot precede IssueDate.";
        if(input.DocumentTypeId.HasValue)
        {
            var type=await db.DocumentTypes.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==input.DocumentTypeId && x.IsActive,ct);
            if(type is null) return "DocumentTypeId must reference an active document type.";
            if(type.RequiresDocumentNumber && string.IsNullOrWhiteSpace(input.DocumentNumber)) return "DocumentNumber is required for this document type.";
            if(type.RequiresExpiryDate && !input.ExpiryDate.HasValue) return "ExpiryDate is required for this document type.";
        }
        if(input.EmploymentRecordId.HasValue && !await db.EmploymentRecords.AsNoTracking().AnyAsync(x=>x.EmployeeId==employeeId && x.EmploymentRecordId==input.EmploymentRecordId,ct)) return "EmploymentRecordId must belong to this employee.";
        if(input.LeaveId.HasValue != input.LeaveEvidenceId.HasValue) return "LeaveId and LeaveEvidenceId must be supplied together.";
        if(input.LeaveEvidenceId.HasValue)
        {
            var evidence=await db.Set<SIAMIS.Domain.Entities.Leave.EmployeeLeaveEvidence>().AsNoTracking().SingleOrDefaultAsync(x=>x.EmployeeId==employeeId && x.LeaveId==input.LeaveId && x.Id==input.LeaveEvidenceId,ct);
            if(evidence is null) return "Leave evidence must belong to this employee and leave.";
            if(input.DocumentTypeId!=evidence.DocumentTypeId) return "DocumentTypeId must match the existing Leave evidence receipt.";
        }
        return null;
    }
    public Task<ServiceResult<HrDocumentDto>> UploadAsync(Guid employeeId, HrDocumentInput input, Stream bytes, string fileName, string contentType, CancellationToken ct)
        => WriteAsync(employeeId,null,null,input,bytes,fileName,contentType,ct);
    public async Task<ServiceResult<HrDocumentDto>> ReplaceAsync(Guid id, Guid? employeeId, string version, Stream bytes, string fileName, string contentType, CancellationToken ct)
    {
        if(!Allowed(true)) return Fail<HrDocumentDto>("forbidden","HR document management is required.");
        var prior=await Owned(id,employeeId).AsNoTracking().SingleOrDefaultAsync(ct);
        if(prior is null) return Fail<HrDocumentDto>("not_found","Document was not found.");
        return await WriteAsync(prior.EmployeeId,id,version,null,bytes,fileName,contentType,ct);
    }
    private async Task<ServiceResult<HrDocumentDto>> WriteAsync(Guid employeeId, Guid? priorId, string? version, HrDocumentInput? input, Stream bytes, string fileName, string contentType, CancellationToken ct)
    {
        if(!Allowed(true) || (input?.LeaveEvidenceId.HasValue==true && !actor.HasCapability("Leave.Evidence"))) return Fail<HrDocumentDto>("forbidden","Document/context management is required.");
        if(!storage.IsConfigured) return Fail<HrDocumentDto>("storage_unavailable","Secure document storage is unavailable.");
        StoredDocument? stored=null;bool committed=false;Guid? attemptedDocumentId=null;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            if(await EmploymentIntegrity.LockAsync(db,employeeId,ct) is null) return Fail<HrDocumentDto>("not_found","Employee was not found.");
            EmployeeDocument? prior=null;
            if(priorId.HasValue)
            {
                prior=await Owned(priorId.Value,employeeId).SingleOrDefaultAsync(ct);
                if(prior is null) return Fail<HrDocumentDto>("not_found","Document was not found.");
                if(!LeaveAllowed(prior)) return Fail<HrDocumentDto>("forbidden","Leave evidence access is required.");
                if(prior.Version!=version || prior.LifecycleStatus is not ("Active" or "MetadataOnly")) return Fail<HrDocumentDto>("conflict","Document version or lifecycle has changed.");
                input=new(prior.Category,prior.DocumentTypeId,prior.EmploymentRecordId,prior.LeaveId,prior.LeaveEvidenceId,prior.Remarks,prior.DocumentNumber,prior.IssueDate,prior.ExpiryDate);
            }
            else
            {
                var invalid=await ValidateAsync(employeeId,input!,ct);
                if(invalid is not null) return Fail<HrDocumentDto>("validation",invalid);
            }
            stored=await storage.StoreAsync(bytes,fileName,contentType,ct);
            var doc=new EmployeeDocument { EmployeeId=employeeId,Category=input!.Category,DocumentTypeId=input.DocumentTypeId,
                EmploymentRecordId=input.EmploymentRecordId,LeaveId=input.LeaveId,LeaveEvidenceId=input.LeaveEvidenceId,Remarks=input.Remarks?.Trim(),
                DocumentNumber=input.DocumentNumber?.Trim(),IssueDate=input.IssueDate,ExpiryDate=input.ExpiryDate,
                FileName=stored.FileName,StorageKey=stored.StorageKey,ContentSha256=stored.ContentSha256,ContentType=stored.ContentType,SizeBytes=stored.SizeBytes,
                CreatedByUserId=actor.UserId,LifecycleStatus="Active",SupersedesDocumentId=priorId };
            if(prior is not null)
            {
                prior.LifecycleStatus="Superseded";prior.Version=Guid.NewGuid().ToString();prior.LifecycleChangedAtUtc=DateTime.UtcNow;prior.LifecycleChangedByUserId=actor.UserId;
                Audit(prior,"DocumentSuperseded");
            }
            attemptedDocumentId=doc.EmployeeDocumentId;
            db.Add(doc);Audit(doc,prior is null ? "DocumentUploaded" : "DocumentReplacementUploaded");
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);committed=true;
            return ServiceResult<HrDocumentDto>.Success(Dto(doc));
        }
        catch(DocumentStorageException e) { return Fail<HrDocumentDto>(e.Code,e.Code=="too_large" ? "Maximum file size is 20 MiB." : e.Code is "validation" or "unsupported_type" ? "Invalid filename, content or file type." : "Secure document storage is unavailable."); }
        catch(DbUpdateConcurrencyException) { return Fail<HrDocumentDto>("conflict","Document version has changed."); }
        catch(DbUpdateException) { return Fail<HrDocumentDto>("storage_unavailable","Document could not be stored safely."); }
        catch(SqlException e) when(e.Number==1205) { return Fail<HrDocumentDto>("conflict","Concurrent document operation; retry with the current version."); }
        finally
        {
            if(!committed)
            {
                // SQL Server already rolls back a deadlock victim; preserve compensating storage cleanup.
                try { await tx.RollbackAsync(CancellationToken.None); }
                catch(Exception e) when(e is SqlException or InvalidOperationException)
                { log.LogWarning("Document database rollback requires operational verification."); }
                db.ChangeTracker.Clear();
                if(stored is not null)
                {
                    // A connection failure during commit can have an uncertain outcome. Never delete
                    // bytes unless a fresh connection confirms that their metadata did not commit.
                    bool safeToRemove=!attemptedDocumentId.HasValue;
                    if(attemptedDocumentId.HasValue)
                    {
                        try
                        {
                            await using var probe=new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>()
                                .UseSqlServer(db.Database.GetConnectionString()).Options);
                            safeToRemove=!await probe.EmployeeDocuments.AsNoTracking().AnyAsync(x=>x.EmployeeDocumentId==attemptedDocumentId,CancellationToken.None);
                        }
                        catch(Exception) { log.LogError("Uncertain document commit requires reconciliation for document {DocumentId}.",attemptedDocumentId); }
                    }
                    if(safeToRemove)
                        try { await storage.RemoveFailedWriteAsync(stored.StorageKey,CancellationToken.None); }
                        catch(DocumentStorageException) { log.LogError("Failed document write requires private-storage reconciliation for document {DocumentId}.",attemptedDocumentId); }
                }
            }
        }
    }
    public async Task<ServiceResult<HrDocumentDto>> ArchiveAsync(Guid id, Guid? employeeId, string version, CancellationToken ct)
    {
        if(!Allowed(true)) return Fail<HrDocumentDto>("forbidden","HR document management is required.");
        var owner=await Owned(id,employeeId).AsNoTracking().Select(x=>(Guid?)x.EmployeeId).SingleOrDefaultAsync(ct);
        if(!owner.HasValue) return Fail<HrDocumentDto>("not_found","Document was not found.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await EmploymentIntegrity.LockAsync(db,owner.Value,ct);
        var doc=await Owned(id,owner).SingleAsync(ct);
        if(!LeaveAllowed(doc)) return Fail<HrDocumentDto>("forbidden","Leave evidence access is required.");
        if(doc.Version!=version || doc.LifecycleStatus is not ("Active" or "MetadataOnly")) return Fail<HrDocumentDto>("conflict","Document version or lifecycle has changed.");
        doc.LifecycleStatus="Archived";doc.Version=Guid.NewGuid().ToString();doc.LifecycleChangedAtUtc=DateTime.UtcNow;doc.LifecycleChangedByUserId=actor.UserId;
        Audit(doc,"DocumentArchived");
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return ServiceResult<HrDocumentDto>.Success(Dto(doc));
    }
    public async Task<ServiceResult<PrivateDocumentContent>> DownloadAsync(Guid id, Guid? employeeId, CancellationToken ct)
    {
        if(!Allowed(false)) return Fail<PrivateDocumentContent>("forbidden","HR document access is required.");
        var doc=await Owned(id,employeeId).AsNoTracking().SingleOrDefaultAsync(ct);
        if(doc is null) return Fail<PrivateDocumentContent>("not_found","Document was not found.");
        if(!LeaveAllowed(doc)) return Fail<PrivateDocumentContent>("forbidden","Leave evidence access is required.");
        Stream? stream=null;
        try
        {
            if(doc.ContentSha256 is null || doc.SizeBytes is null || doc.ContentType is not ("application/pdf" or "image/jpeg" or "image/png")) throw new DocumentStorageException("storage_unavailable");
            var name=PrivateDocumentStorage.ValidateFileName(doc.FileName);
            stream=await storage.OpenVerifiedAsync(doc.StorageKey,doc.ContentSha256,doc.SizeBytes.Value,ct);
            Audit(doc,"DocumentDownloaded");await db.SaveChangesAsync(ct);
            return ServiceResult<PrivateDocumentContent>.Success(new(stream,doc.ContentType,name));
        }
        catch(DocumentStorageException)
        {
            if(stream is not null) await stream.DisposeAsync();
            Audit(doc,"DocumentContentUnavailable");await db.SaveChangesAsync(ct);
            log.LogWarning("Private document integrity/availability failure for document {DocumentId}.",id);
            return Fail<PrivateDocumentContent>("storage_unavailable","Document content is unavailable or failed integrity verification.");
        }
        catch { if(stream is not null) await stream.DisposeAsync();throw; }
    }
}
