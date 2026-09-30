using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeAddressesService(SIAMISDbContext db) : IEmployeeAddressesService
{
    public async Task<ServiceResult<IReadOnlyList<EmployeeAddressDto>>> GetAddressesAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<IReadOnlyList<EmployeeAddressDto>>("not_found", "Employee was not found.");

        var addresses = await db.EmployeeAddresses.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.EmployeeAddressId)
            .Select(item => new EmployeeAddressDto
            {
                EmployeeAddressId = item.EmployeeAddressId,
                AddressTypeId = item.AddressTypeId,
                AddressType = item.AddressType.Name,
                AddressLine1 = item.AddressLine1,
                AddressLine2 = item.AddressLine2,
                City = item.City,
                StateProvince = item.StateProvince,
                CountryId = item.CountryId,
                Country = item.Country == null ? null : item.Country.Name,
                PostalCode = item.PostalCode,
                IsPrimary = item.IsPrimary
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<EmployeeAddressDto>>.Success(addresses);
    }

    public async Task<ServiceResult<EmployeeAddressDto>> CreateAddressAsync(Guid employeeId, CreateEmployeeAddressRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<EmployeeAddressDto>("not_found", "Employee was not found.");

        if (!await IsActiveAddressType(request.AddressTypeId!.Value, cancellationToken))
            return Fail<EmployeeAddressDto>("validation", "AddressTypeId must reference an active address type.");
        if (request.CountryId.HasValue && !await IsActiveCountry(request.CountryId.Value, cancellationToken))
            return Fail<EmployeeAddressDto>("validation", "CountryId must reference an active country.");

        var existing = await db.EmployeeAddresses.Where(item => item.EmployeeId == employeeId)
            .OrderBy(item => item.EmployeeAddressId).ToListAsync(cancellationToken);
        var address = new EmployeeAddress
        {
            EmployeeId = employeeId,
            AddressTypeId = request.AddressTypeId.Value,
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = Clean(request.AddressLine2),
            City = Clean(request.City),
            StateProvince = Clean(request.StateProvince),
            CountryId = request.CountryId,
            PostalCode = Clean(request.PostalCode),
            IsPrimary = request.IsPrimary == true || existing.Count == 0
        };

        if (address.IsPrimary)
        {
            var previousPrimary = existing.Where(item => item.IsPrimary).ToList();
            foreach (var item in previousPrimary)
                item.IsPrimary = false;
            if (previousPrimary.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }

        db.EmployeeAddresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<EmployeeAddressDto>.Success(await GetAddressDtoAsync(address.EmployeeAddressId, cancellationToken));
    }

    public async Task<ServiceResult<EmployeeAddressDto>> UpdateAddressAsync(Guid employeeId, Guid addressId, UpdateEmployeeAddressRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<EmployeeAddressDto>("not_found", "Employee was not found.");

        var address = await db.EmployeeAddresses.SingleOrDefaultAsync(
            item => item.EmployeeId == employeeId && item.EmployeeAddressId == addressId, cancellationToken);
        if (address is null)
            return Fail<EmployeeAddressDto>("not_found", "Address was not found for this employee.");

        if (request.AddressTypeId.HasValue)
        {
            if (!await IsActiveAddressType(request.AddressTypeId.Value, cancellationToken))
                return Fail<EmployeeAddressDto>("validation", "AddressTypeId must reference an active address type.");
            address.AddressTypeId = request.AddressTypeId.Value;
        }
        if (request.AddressLine1 is not null)
        {
            if (string.IsNullOrWhiteSpace(request.AddressLine1))
                return Fail<EmployeeAddressDto>("validation", "AddressLine1 is required.");
            address.AddressLine1 = request.AddressLine1.Trim();
        }
        address.AddressLine2 = PreserveOrReplace(address.AddressLine2, request.AddressLine2);
        address.City = PreserveOrReplace(address.City, request.City);
        address.StateProvince = PreserveOrReplace(address.StateProvince, request.StateProvince);
        address.PostalCode = PreserveOrReplace(address.PostalCode, request.PostalCode);
        if (request.CountryId.HasValue)
        {
            if (!await IsActiveCountry(request.CountryId.Value, cancellationToken))
                return Fail<EmployeeAddressDto>("validation", "CountryId must reference an active country.");
            address.CountryId = request.CountryId.Value;
        }

        var others = await db.EmployeeAddresses.Where(item => item.EmployeeId == employeeId && item.EmployeeAddressId != addressId)
            .OrderBy(item => item.EmployeeAddressId).ToListAsync(cancellationToken);

        if (request.IsPrimary == true)
        {
            var previousPrimary = others.Where(item => item.IsPrimary).ToList();
            foreach (var item in previousPrimary)
                item.IsPrimary = false;
            if (previousPrimary.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
            address.IsPrimary = true;
        }
        else if (request.IsPrimary == false && address.IsPrimary && others.Count > 0)
        {
            address.IsPrimary = false;
            await db.SaveChangesAsync(cancellationToken);
            others[0].IsPrimary = true;
        }
        // Keep a sole address primary when an update tries to unmark it.

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<EmployeeAddressDto>.Success(await GetAddressDtoAsync(addressId, cancellationToken));
    }

    public async Task<ServiceResult<bool>> DeleteAddressAsync(Guid employeeId, Guid addressId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<bool>("not_found", "Employee was not found.");

        var address = await db.EmployeeAddresses.SingleOrDefaultAsync(
            item => item.EmployeeId == employeeId && item.EmployeeAddressId == addressId, cancellationToken);
        if (address is null)
            return Fail<bool>("not_found", "Address was not found for this employee.");

        var nextPrimary = address.IsPrimary
            ? await db.EmployeeAddresses.Where(item => item.EmployeeId == employeeId && item.EmployeeAddressId != addressId)
                .OrderBy(item => item.EmployeeAddressId).FirstOrDefaultAsync(cancellationToken)
            : null;

        db.EmployeeAddresses.Remove(address);
        await db.SaveChangesAsync(cancellationToken);
        if (nextPrimary is not null)
        {
            nextPrimary.IsPrimary = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private Task<bool> IsActiveAddressType(Guid id, CancellationToken cancellationToken)
        => db.AddressTypes.AsNoTracking().AnyAsync(item => item.Id == id && item.IsActive, cancellationToken);

    private Task<bool> IsActiveCountry(Guid id, CancellationToken cancellationToken)
        => db.Countries.AsNoTracking().AnyAsync(item => item.Id == id && item.IsActive, cancellationToken);

    private Task<EmployeeAddressDto> GetAddressDtoAsync(Guid addressId, CancellationToken cancellationToken)
        => db.EmployeeAddresses.AsNoTracking().Where(item => item.EmployeeAddressId == addressId)
            .Select(item => new EmployeeAddressDto
            {
                EmployeeAddressId = item.EmployeeAddressId,
                AddressTypeId = item.AddressTypeId,
                AddressType = item.AddressType.Name,
                AddressLine1 = item.AddressLine1,
                AddressLine2 = item.AddressLine2,
                City = item.City,
                StateProvince = item.StateProvince,
                CountryId = item.CountryId,
                Country = item.Country == null ? null : item.Country.Name,
                PostalCode = item.PostalCode,
                IsPrimary = item.IsPrimary
            })
            .SingleAsync(cancellationToken);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? PreserveOrReplace(string? existing, string? requested)
        => requested is null ? existing : Clean(requested);

    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
}
