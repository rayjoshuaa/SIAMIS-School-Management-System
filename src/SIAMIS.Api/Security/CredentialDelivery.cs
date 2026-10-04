using System.Collections.Concurrent;
using SIAMIS.Application.Security;

namespace SIAMIS.Api.Security;

/// <summary>No provider selected: never pretend instructions were sent or disclose production credentials.</summary>
public sealed class UnconfiguredCredentialDelivery : ICredentialDelivery
{
    public bool IsConfigured => false;
    public Task<bool> DeliverAsync(CredentialDeliveryMessage message, CancellationToken ct) => Task.FromResult(false);
}
/// <summary>Explicit local testing handoff, RAM only, short-lived and consumed on collection. No file/log/DB persistence.</summary>
public sealed class DevelopmentCredentialDelivery : ICredentialDelivery
{
    private readonly ConcurrentDictionary<Guid, (DateTimeOffset Expires, CredentialDeliveryMessage Message)> pending = new();
    public bool IsConfigured => true;
    public Task<bool> DeliverAsync(CredentialDeliveryMessage message, CancellationToken ct)
    {
        foreach (var item in pending.Where(x => x.Value.Expires < DateTimeOffset.UtcNow)) pending.TryRemove(item.Key, out _);
        pending[message.UserId] = (DateTimeOffset.UtcNow.AddMinutes(15), message);return Task.FromResult(true);
    }
    public CredentialDeliveryMessage? Collect(Guid id) => pending.TryRemove(id, out var value) && value.Expires >= DateTimeOffset.UtcNow ? value.Message : null;
}
