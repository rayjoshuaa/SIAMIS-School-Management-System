namespace SIAMIS.Domain.Entities.Employees;

// Immutable binary provenance. Only the current-head marker changes on later commands.
public sealed class EmployeePhotoRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public bool IsCurrent { get; set; } = true;
    public string? StorageKey { get; set; }
    public string? Sha256 { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Operation { get; set; } = "Upload";
}
