using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities;

/// <summary>Optional configuration for the single school/employer; absence means unconfigured.</summary>
public sealed class OrganizationProfile : IHasTimestamps
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");
    public Guid OrganizationProfileId { get; set; } = SingletonId;
    public string DisplayName { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
