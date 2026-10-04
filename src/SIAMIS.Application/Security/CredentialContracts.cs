using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Security;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ForgotPasswordRequest
{
    // Optional input deliberately shares the generic outcome with accounts lacking a recovery email.
    [StringLength(256)] public string? Email { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CompleteCredentialRequest
{
    [Required] public Guid? UserId { get; set; }
    [Required, StringLength(4096)] public string Token { get; set; } = "";
    [Required, StringLength(256)] public string NewPassword { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class IssueCredentialRequest
{
    [Required, StringLength(36)] public string Version { get; set; } = "";
}
public sealed record CredentialDeliveryMessage(Guid UserId, string Email, string Purpose, string Token);
public sealed record CredentialIssuedDto(string Purpose);
public interface ICredentialDelivery
{
    bool IsConfigured { get; }
    Task<bool> DeliverAsync(CredentialDeliveryMessage message, CancellationToken ct);
}
public interface ICredentialService
{
    Task<bool> DeliverInitialActivationAsync(Guid userId, CancellationToken ct);
    Task ForgotAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task<ServiceResult<CredentialIssuedDto>> IssueAsync(Guid userId, IssueCredentialRequest request, CancellationToken ct);
    Task<bool> CompleteAsync(CompleteCredentialRequest request, bool activation, CancellationToken ct);
}
