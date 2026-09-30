using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeEmergencyContactsService(SIAMISDbContext db) : IEmployeeEmergencyContactsService
{
    public async Task<ServiceResult<IReadOnlyList<EmployeeEmergencyContactDto>>> GetEmergencyContactsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<IReadOnlyList<EmployeeEmergencyContactDto>>("not_found", "Employee was not found.");

        var contacts = await db.EmergencyContacts.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.EmergencyContactId)
            .Select(item => new EmployeeEmergencyContactDto
            {
                EmergencyContactId = item.EmergencyContactId,
                EmployeeId = item.EmployeeId,
                Name = item.Name,
                Relationship = item.Relationship,
                Phone = item.Phone,
                AlternativePhone = item.AlternativePhone,
                Address = item.Address,
                IsPrimary = item.IsPrimary
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<EmployeeEmergencyContactDto>>.Success(contacts);
    }

    public async Task<ServiceResult<EmployeeEmergencyContactDto>> CreateEmergencyContactAsync(Guid employeeId, CreateEmergencyContactRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<EmployeeEmergencyContactDto>("not_found", "Employee was not found.");

        var existing = await db.EmergencyContacts.Where(item => item.EmployeeId == employeeId)
            .OrderBy(item => item.EmergencyContactId).ToListAsync(cancellationToken);
        var contact = new EmergencyContact
        {
            EmployeeId = employeeId,
            Name = request.Name.Trim(),
            Relationship = request.Relationship.Trim(),
            Phone = request.Phone.Trim(),
            AlternativePhone = Clean(request.AlternativePhone),
            Address = Clean(request.Address),
            IsPrimary = request.IsPrimary == true || existing.Count == 0
        };

        if (contact.IsPrimary)
        {
            var previousPrimary = existing.Where(item => item.IsPrimary).ToList();
            foreach (var item in previousPrimary)
                item.IsPrimary = false;
            if (previousPrimary.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }

        db.EmergencyContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<EmployeeEmergencyContactDto>.Success(ToDto(contact));
    }

    public async Task<ServiceResult<EmployeeEmergencyContactDto>> UpdateEmergencyContactAsync(Guid employeeId, Guid emergencyContactId, UpdateEmergencyContactRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<EmployeeEmergencyContactDto>("not_found", "Employee was not found.");

        var contact = await db.EmergencyContacts.SingleOrDefaultAsync(
            item => item.EmployeeId == employeeId && item.EmergencyContactId == emergencyContactId, cancellationToken);
        if (contact is null)
            return Fail<EmployeeEmergencyContactDto>("not_found", "Emergency contact was not found for this employee.");

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Fail<EmployeeEmergencyContactDto>("validation", "Name is required.");
            contact.Name = request.Name.Trim();
        }
        if (request.Relationship is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Relationship))
                return Fail<EmployeeEmergencyContactDto>("validation", "Relationship is required.");
            contact.Relationship = request.Relationship.Trim();
        }
        if (request.Phone is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Phone))
                return Fail<EmployeeEmergencyContactDto>("validation", "Phone is required.");
            contact.Phone = request.Phone.Trim();
        }
        if (request.AlternativePhone is not null)
            contact.AlternativePhone = Clean(request.AlternativePhone);
        if (request.Address is not null)
            contact.Address = Clean(request.Address);

        var others = await db.EmergencyContacts.Where(item => item.EmployeeId == employeeId && item.EmergencyContactId != emergencyContactId)
            .OrderBy(item => item.EmergencyContactId).ToListAsync(cancellationToken);

        if (request.IsPrimary == true)
        {
            var previousPrimary = others.Where(item => item.IsPrimary).ToList();
            foreach (var item in previousPrimary)
                item.IsPrimary = false;
            if (previousPrimary.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
            contact.IsPrimary = true;
        }
        else if (request.IsPrimary == false && contact.IsPrimary && others.Count > 0)
        {
            contact.IsPrimary = false;
            await db.SaveChangesAsync(cancellationToken);
            others[0].IsPrimary = true;
        }
        // Keep a sole emergency contact primary when an update tries to unmark it.

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<EmployeeEmergencyContactDto>.Success(ToDto(contact));
    }

    public async Task<ServiceResult<bool>> DeleteEmergencyContactAsync(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<bool>("not_found", "Employee was not found.");

        var contact = await db.EmergencyContacts.SingleOrDefaultAsync(
            item => item.EmployeeId == employeeId && item.EmergencyContactId == emergencyContactId, cancellationToken);
        if (contact is null)
            return Fail<bool>("not_found", "Emergency contact was not found for this employee.");

        var nextPrimary = contact.IsPrimary
            ? await db.EmergencyContacts.Where(item => item.EmployeeId == employeeId && item.EmergencyContactId != emergencyContactId)
                .OrderBy(item => item.EmergencyContactId).FirstOrDefaultAsync(cancellationToken)
            : null;

        db.EmergencyContacts.Remove(contact);
        await db.SaveChangesAsync(cancellationToken);
        if (nextPrimary is not null)
        {
            nextPrimary.IsPrimary = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EmployeeEmergencyContactDto ToDto(EmergencyContact contact) => new()
    {
        EmergencyContactId = contact.EmergencyContactId,
        EmployeeId = contact.EmployeeId,
        Name = contact.Name,
        Relationship = contact.Relationship,
        Phone = contact.Phone,
        AlternativePhone = contact.AlternativePhone,
        Address = contact.Address,
        IsPrimary = contact.IsPrimary
    };

    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
}
