using SIAMIS.Domain.Entities.MasterData;

namespace SIAMIS.Domain.Entities.Employees;

public sealed class EmployeeDocument
{
    public Guid EmployeeDocumentId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public string? DocumentNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = "Pending";
    public Guid? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? Remarks { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = "GeneralHRDocument";
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? ContentSha256 { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? EmploymentRecordId { get; set; }
    public Guid? LeaveId { get; set; }
    public Guid? LeaveEvidenceId { get; set; }
    public Guid? SupersedesDocumentId { get; set; }
    public string LifecycleStatus { get; set; } = "MetadataOnly";
    public string Version { get; set; } = Guid.NewGuid().ToString();
    public DateTime? LifecycleChangedAtUtc { get; set; }
    public Guid? LifecycleChangedByUserId { get; set; }

    public Employee Employee { get; set; } = null!;
    public Employee? Verifier { get; set; }
    public DocumentType? DocumentType { get; set; }
}
