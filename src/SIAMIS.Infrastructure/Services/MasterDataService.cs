using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.MasterData;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class MasterDataService(SIAMISDbContext db) : IMasterDataService
{
    public Task<IReadOnlyList<MasterDataItemDto>> GetDepartmentsAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<Department>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetDesignationsAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<Designation>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetEmploymentTypesAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<EmploymentType>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetEmploymentStatusesAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<EmploymentStatus>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetLocationsAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<Location>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetHiringSourcesAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<HiringSource>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetGendersAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<Gender>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetMaritalStatusesAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<MaritalStatus>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetNationalitiesAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<Nationality>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetAddressTypesAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<AddressType>(includeInactive, cancellationToken);

    public Task<IReadOnlyList<MasterDataItemDto>> GetCountriesAsync(bool includeInactive, CancellationToken cancellationToken)
        => GetAsync<Country>(includeInactive, cancellationToken);

    private async Task<IReadOnlyList<MasterDataItemDto>> GetAsync<T>(bool includeInactive, CancellationToken cancellationToken)
        where T : MasterDataEntity
    {
        IQueryable<T> query = db.Set<T>().AsNoTracking();
        if (!includeInactive)
            query = query.Where(item => item.IsActive);

        return await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => new MasterDataItemDto(
                item.Id,
                item.Code,
                item.Name,
                item.Description,
                item.IsActive))
            .ToListAsync(cancellationToken);
    }
}
