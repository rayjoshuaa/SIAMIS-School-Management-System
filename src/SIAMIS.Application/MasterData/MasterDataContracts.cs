namespace SIAMIS.Application.MasterData;

/// <summary>A public, navigation-free representation of a master-data value.</summary>
public sealed record MasterDataItemDto(
    Guid Id,
    string? Code,
    string Name,
    string? Description,
    bool IsActive);

/// <summary>Read-only queries for the master data used by employee management.</summary>
public interface IMasterDataService
{
    Task<IReadOnlyList<MasterDataItemDto>> GetDepartmentsAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetDesignationsAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetEmploymentTypesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<EmploymentStatusDto>> GetEmploymentStatusesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetLocationsAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetHiringSourcesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetGendersAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetMaritalStatusesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetNationalitiesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetAddressTypesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<IReadOnlyList<MasterDataItemDto>> GetCountriesAsync(bool includeInactive, CancellationToken cancellationToken);
}
