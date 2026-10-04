using Microsoft.AspNetCore.Identity;

namespace SIAMIS.Infrastructure.Security;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresPasswordChange { get; set; } = true;
    public string AdministrationVersion { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Minimal immutable audit; contains no passwords, cookies, tokens or confidential payloads.</summary>
public sealed class SecurityAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
