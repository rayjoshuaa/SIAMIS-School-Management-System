namespace SIAMIS.Domain.Entities.Employees;

// Durable operation receipt. No cascading identity/employee relationships: a retired
// registration key must remain reserved even when an eligible employee is deleted.
public sealed class EmployeeRegistrationReceipt
{
    public Guid ActorUserId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Guid RequestKey { get; set; }
    public byte[] PayloadHash { get; set; } = [];
    public Guid ResultEmployeeId { get; set; }
    public string ResultEmployeeNumber { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public DateTime ReplayUntilUtc { get; set; }
    // Only the original authorized response, never the submitted request or credentials.
    // Null after expiry retains the permanent key/hash tombstone without response PII.
    public string? ResponseJson { get; set; }
}
