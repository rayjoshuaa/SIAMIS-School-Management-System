using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeContactsService(SIAMISDbContext db) : IEmployeeContactsService
{
    public async Task<ServiceResult<IReadOnlyList<EmployeeContactDto>>> GetContactsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<IReadOnlyList<EmployeeContactDto>>("not_found", "Employee was not found.");

        var contacts = await db.EmployeeContacts.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.EmployeeContactId)
            .Select(item => new EmployeeContactDto
            {
                EmployeeContactId = item.EmployeeContactId,
                WorkEmail = item.WorkEmail,
                PersonalEmail = item.PersonalEmail,
                Mobile = item.Mobile,
                Phone = item.Phone,
                WorkPhone = item.WorkPhone,
                IsPrimary = item.IsPrimary
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<EmployeeContactDto>>.Success(contacts);
    }

    public async Task<ServiceResult<EmployeeContactDto>> CreateContactAsync(Guid employeeId, CreateEmployeeContactRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<EmployeeContactDto>("not_found", "Employee was not found.");

        var existing = await db.EmployeeContacts.Where(item => item.EmployeeId == employeeId)
            .OrderBy(item => item.EmployeeContactId).ToListAsync(cancellationToken);
        var contact = new EmployeeContact
        {
            EmployeeId = employeeId,
            WorkEmail = Clean(request.WorkEmail),
            PersonalEmail = Clean(request.PersonalEmail),
            Mobile = Clean(request.Mobile),
            Phone = Clean(request.Phone),
            WorkPhone = Clean(request.WorkPhone),
            IsPrimary = request.IsPrimary || existing.Count == 0
        };

        if (!HasAnyValue(contact))
            return Fail<EmployeeContactDto>("validation", "At least one contact value must be provided.");
        if (existing.Any(item => SameValues(item, contact)))
            return Fail<EmployeeContactDto>("conflict", "An identical contact already exists for this employee.");

        if (contact.IsPrimary)
        {
            var previousPrimary = existing.Where(item => item.IsPrimary).ToList();
            foreach (var item in previousPrimary)
                item.IsPrimary = false;
            if (previousPrimary.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }

        db.EmployeeContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<EmployeeContactDto>.Success(ToDto(contact));
    }

    public async Task<ServiceResult<EmployeeContactDto>> UpdateContactAsync(Guid employeeId, Guid contactId, UpdateEmployeeContactRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<EmployeeContactDto>("not_found", "Employee was not found.");

        var contact = await db.EmployeeContacts.SingleOrDefaultAsync(
            item => item.EmployeeId == employeeId && item.EmployeeContactId == contactId, cancellationToken);
        if (contact is null)
            return Fail<EmployeeContactDto>("not_found", "Contact was not found for this employee.");

        contact.WorkEmail = PreserveOrReplace(contact.WorkEmail, request.WorkEmail);
        contact.PersonalEmail = PreserveOrReplace(contact.PersonalEmail, request.PersonalEmail);
        contact.Mobile = PreserveOrReplace(contact.Mobile, request.Mobile);
        contact.Phone = PreserveOrReplace(contact.Phone, request.Phone);
        contact.WorkPhone = PreserveOrReplace(contact.WorkPhone, request.WorkPhone);

        if (!HasAnyValue(contact))
            return Fail<EmployeeContactDto>("validation", "At least one contact value must be provided.");

        var others = await db.EmployeeContacts.Where(item => item.EmployeeId == employeeId && item.EmployeeContactId != contactId)
            .OrderBy(item => item.EmployeeContactId).ToListAsync(cancellationToken);

        if (request.IsPrimary == true)
        {
            var previousPrimary = others.Where(item => item.IsPrimary).ToList();
            foreach (var item in previousPrimary)
                item.IsPrimary = false;
            if (previousPrimary.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
            contact.IsPrimary = true;
        }
        else if (request.IsPrimary == false && contact.IsPrimary)
        {
            contact.IsPrimary = false;
            if (others.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                others[0].IsPrimary = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<EmployeeContactDto>.Success(ToDto(contact));
    }

    public async Task<ServiceResult<bool>> DeleteContactAsync(Guid employeeId, Guid contactId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await db.Employees.AnyAsync(item => item.EmployeeId == employeeId, cancellationToken))
            return Fail<bool>("not_found", "Employee was not found.");

        var contact = await db.EmployeeContacts.SingleOrDefaultAsync(
            item => item.EmployeeId == employeeId && item.EmployeeContactId == contactId, cancellationToken);
        if (contact is null)
            return Fail<bool>("not_found", "Contact was not found for this employee.");

        var wasPrimary = contact.IsPrimary;
        var nextPrimary = wasPrimary
            ? await db.EmployeeContacts.Where(item => item.EmployeeId == employeeId && item.EmployeeContactId != contactId)
                .OrderBy(item => item.EmployeeContactId).FirstOrDefaultAsync(cancellationToken)
            : null;

        db.EmployeeContacts.Remove(contact);
        await db.SaveChangesAsync(cancellationToken);
        if (nextPrimary is not null)
        {
            nextPrimary.IsPrimary = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private static string? PreserveOrReplace(string? existing, string? requested)
        => string.IsNullOrWhiteSpace(requested) ? existing : requested.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasAnyValue(EmployeeContact contact)
        => contact.WorkEmail is not null || contact.PersonalEmail is not null || contact.Mobile is not null
            || contact.Phone is not null || contact.WorkPhone is not null;

    private static bool SameValues(EmployeeContact left, EmployeeContact right)
        => Same(left.WorkEmail, right.WorkEmail) && Same(left.PersonalEmail, right.PersonalEmail)
            && Same(left.Mobile, right.Mobile) && Same(left.Phone, right.Phone) && Same(left.WorkPhone, right.WorkPhone);

    private static bool Same(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static EmployeeContactDto ToDto(EmployeeContact contact) => new()
    {
        EmployeeContactId = contact.EmployeeContactId,
        WorkEmail = contact.WorkEmail,
        PersonalEmail = contact.PersonalEmail,
        Mobile = contact.Mobile,
        Phone = contact.Phone,
        WorkPhone = contact.WorkPhone,
        IsPrimary = contact.IsPrimary
    };

    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
}
