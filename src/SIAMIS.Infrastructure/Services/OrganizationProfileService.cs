using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class OrganizationProfileService(SIAMISDbContext db) : IOrganizationProfileService
{
    public async Task<OrganizationProfileDto?> GetAsync(CancellationToken ct)
    {
        var row = await db.OrganizationProfiles.AsNoTracking().SingleOrDefaultAsync(ct);
        return row is null ? null : ToDto(row);
    }

    public async Task<OrganizationProfileDto> PutAsync(OrganizationProfileRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.OrganizationProfiles.FromSqlInterpolated(
            $"SELECT * FROM [OrganizationProfiles] WITH (UPDLOCK) WHERE [OrganizationProfileId] = {OrganizationProfile.SingletonId}").SingleOrDefaultAsync(ct);
        if (row is null) { row = new(); db.OrganizationProfiles.Add(row); }
        row.DisplayName = request.DisplayName.Trim();
        row.AddressLine1 = Clean(request.AddressLine1); row.AddressLine2 = Clean(request.AddressLine2);
        row.Phone = Clean(request.Phone); row.Email = Clean(request.Email);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(row);
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static OrganizationProfileDto ToDto(OrganizationProfile x) => new(x.OrganizationProfileId, x.DisplayName,
        x.AddressLine1, x.AddressLine2, x.Phone, x.Email,
        DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), DateTime.SpecifyKind(x.UpdatedAt, DateTimeKind.Utc));
}
