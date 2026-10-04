using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace SIAMIS.Application.Employees;

public sealed record HrDocumentInput(string Category,Guid? DocumentTypeId,Guid? EmploymentRecordId,Guid? LeaveId,Guid? LeaveEvidenceId,string? Remarks,string? DocumentNumber=null,DateOnly? IssueDate=null,DateOnly? ExpiryDate=null);
public sealed record HrDocumentDto(Guid EmployeeDocumentId,Guid EmployeeId,Guid? DocumentTypeId,Guid? EmploymentRecordId,Guid? LeaveId,Guid? LeaveEvidenceId,
    string Category,string FileName,string? ContentType,long? SizeBytes,string? ContentSha256,Guid? CreatedByUserId,DateTime UploadedAt,
    string LifecycleStatus,string Version,Guid? SupersedesDocumentId,DateTime? LifecycleChangedAtUtc,Guid? LifecycleChangedByUserId,string? Remarks,string? DocumentNumber,DateOnly? IssueDate,DateOnly? ExpiryDate,string VerificationStatus,Guid? VerifiedBy,DateTime? VerifiedAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class HrDocumentLifecycleRequest
{
    [Required, StringLength(36, MinimumLength=36)]
    public string Version { get; set; }="";
}
public sealed record PrivateDocumentContent(Stream Stream,string ContentType,string FileName);
public sealed record StoredDocument(string StorageKey,string ContentSha256,long SizeBytes,string ContentType,string FileName);
public interface IPrivateDocumentStorage
{
    bool IsConfigured { get; }
    Task<StoredDocument> StoreAsync(Stream stream,string fileName,string declaredContentType,CancellationToken ct);
    Task<Stream> OpenVerifiedAsync(string key,string sha256,long size,CancellationToken ct);
    Task RemoveFailedWriteAsync(string key,CancellationToken ct);
}
public sealed class DocumentStorageException(string code) : Exception("Private document operation failed.")
{
    public string Code { get; }=code;
}
public interface IHrDocumentService
{
    Task<ServiceResult<IReadOnlyList<HrDocumentDto>>> ListAsync(Guid employeeId,bool includeHistory,string? category,CancellationToken ct);
    Task<ServiceResult<HrDocumentDto>> GetAsync(Guid id,Guid? employeeId,CancellationToken ct);
    Task<ServiceResult<HrDocumentDto>> UploadAsync(Guid employeeId,HrDocumentInput input,Stream bytes,string fileName,string contentType,CancellationToken ct);
    Task<ServiceResult<HrDocumentDto>> ReplaceAsync(Guid id,Guid? employeeId,string version,Stream bytes,string fileName,string contentType,CancellationToken ct);
    Task<ServiceResult<HrDocumentDto>> ArchiveAsync(Guid id,Guid? employeeId,string version,CancellationToken ct);
    Task<ServiceResult<PrivateDocumentContent>> DownloadAsync(Guid id,Guid? employeeId,CancellationToken ct);
}
