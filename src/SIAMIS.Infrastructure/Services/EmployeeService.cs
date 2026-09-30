using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeService(SIAMISDbContext db) : IEmployeeService
{
    public async Task<PagedResult<EmployeeListItemDto>> GetEmployeesAsync(EmployeeListQuery query, CancellationToken cancellationToken)
    {
        var employees = db.Employees.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            employees = employees.Where(item => item.EmployeeNumber.Contains(search)
                || item.FirstName.Contains(search)
                || item.LastName.Contains(search)
                || (item.PreferredName != null && item.PreferredName.Contains(search)));
        }
        if (query.DepartmentId is Guid departmentId)
            employees = employees.Where(item => item.EmploymentRecords.Any(record => record.IsCurrent && record.DepartmentId == departmentId));
        if (query.DesignationId is Guid designationId)
            employees = employees.Where(item => item.EmploymentRecords.Any(record => record.IsCurrent && record.DesignationId == designationId));
        if (query.EmploymentStatusId is Guid statusId)
            employees = employees.Where(item => item.EmploymentRecords.Any(record => record.IsCurrent && record.EmploymentStatusId == statusId));
        if (query.IsActive is bool isActive)
            employees = employees.Where(item => item.IsActive == isActive);

        var totalCount = await employees.CountAsync(cancellationToken);
        var items = await employees
            .OrderBy(item => item.EmployeeNumber).ThenBy(item => item.EmployeeId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new EmployeeListItemDto
            {
                EmployeeId = item.EmployeeId,
                EmployeeNumber = item.EmployeeNumber,
                FirstName = item.FirstName,
                MiddleName = item.MiddleName,
                LastName = item.LastName,
                PreferredName = item.PreferredName,
                IsActive = item.IsActive,
                ProfilePhoto = item.ProfilePhoto,
                HireDate = item.EmploymentRecords.Where(record => record.IsCurrent).Select(record => (DateOnly?)record.HireDate).FirstOrDefault(),
                Department = item.EmploymentRecords.Where(record => record.IsCurrent).Select(record => record.Department == null ? null : record.Department.Name).FirstOrDefault(),
                Designation = item.EmploymentRecords.Where(record => record.IsCurrent).Select(record => record.Designation == null ? null : record.Designation.Name).FirstOrDefault(),
                EmploymentStatus = item.EmploymentRecords.Where(record => record.IsCurrent).Select(record => record.EmploymentStatus == null ? null : record.EmploymentStatus.Name).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<EmployeeDetailDto?> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await EmployeeGraph().AsNoTracking().SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);
        return employee is null ? null : Map(employee);
    }

    public async Task<ServiceResult<EmployeeDetailDto>> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateWriteRequest(request, null, cancellationToken);
        if (validation is not null) return validation;
        var employeeNumber = request.EmployeeNumber.Trim();
        if (await db.Employees.AnyAsync(item => item.EmployeeNumber == employeeNumber, cancellationToken))
            return ServiceResult<EmployeeDetailDto>.Fail("conflict", "EmployeeNumber is already in use.");

        var employee = new Employee
        {
            EmployeeNumber = employeeNumber,
            FirstName = request.FirstName.Trim(), MiddleName = Clean(request.MiddleName), LastName = request.LastName.Trim(),
            PreferredName = Clean(request.PreferredName), DateOfBirth = request.DateOfBirth,
            GenderId = request.GenderId, MaritalStatusId = request.MaritalStatusId, NationalityId = request.NationalityId,
            ProfilePhoto = Clean(request.ProfilePhoto), IsActive = true
        };
        employee.EmploymentRecords.Add(CreateEmployment(request));
        AddChildren(employee, request);
        db.Employees.Add(employee);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsEmployeeNumberConflict(exception))
        {
            return ServiceResult<EmployeeDetailDto>.Fail("conflict", "EmployeeNumber is already in use.");
        }
        return ServiceResult<EmployeeDetailDto>.Success(Map(await EmployeeGraph().SingleAsync(item => item.EmployeeId == employee.EmployeeId, cancellationToken)));
    }

    public async Task<ServiceResult<EmployeeDetailDto>> UpdateEmployeeAsync(Guid employeeId, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await EmployeeGraph().SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);
        if (employee is null) return ServiceResult<EmployeeDetailDto>.Fail("not_found", "Employee was not found.");
        var validation = await ValidateWriteRequest(request, employeeId, cancellationToken);
        if (validation is not null) return validation;
        var employeeNumber = request.EmployeeNumber.Trim();
        if (await db.Employees.AnyAsync(item => item.EmployeeId != employeeId && item.EmployeeNumber == employeeNumber, cancellationToken))
            return ServiceResult<EmployeeDetailDto>.Fail("conflict", "EmployeeNumber is already in use.");

        employee.EmployeeNumber = employeeNumber;
        employee.FirstName = request.FirstName.Trim();
        employee.MiddleName = Clean(request.MiddleName);
        employee.LastName = request.LastName.Trim();
        employee.PreferredName = Clean(request.PreferredName);
        employee.DateOfBirth = request.DateOfBirth;
        employee.GenderId = request.GenderId;
        employee.MaritalStatusId = request.MaritalStatusId;
        employee.NationalityId = request.NationalityId;
        employee.ProfilePhoto = Clean(request.ProfilePhoto);

        var current = employee.EmploymentRecords.SingleOrDefault(record => record.IsCurrent);
        if (current is null) employee.EmploymentRecords.Add(CreateEmployment(request));
        else UpdateEmployment(current, request);

        // A supplied collection replaces that collection; an omitted collection is left intact.
        if (request.Contacts is not null)
        {
            db.EmployeeContacts.RemoveRange(employee.Contacts);
            foreach (var item in request.Contacts) employee.Contacts.Add(ToContact(item));
        }
        if (request.Addresses is not null)
        {
            db.EmployeeAddresses.RemoveRange(employee.Addresses);
            foreach (var item in request.Addresses) employee.Addresses.Add(ToAddress(item));
        }
        if (request.EmergencyContacts is not null)
        {
            db.EmergencyContacts.RemoveRange(employee.EmergencyContacts);
            foreach (var item in request.EmergencyContacts) employee.EmergencyContacts.Add(ToEmergencyContact(item));
        }
        if (request.TeacherProfile is not null)
        {
            if (employee.TeacherProfile is null) employee.TeacherProfile = ToTeacherProfile(request.TeacherProfile);
            else UpdateTeacherProfile(employee.TeacherProfile, request.TeacherProfile);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsEmployeeNumberConflict(exception))
        {
            return ServiceResult<EmployeeDetailDto>.Fail("conflict", "EmployeeNumber is already in use.");
        }
        return ServiceResult<EmployeeDetailDto>.Success(Map(await EmployeeGraph().AsNoTracking().SingleAsync(item => item.EmployeeId == employeeId, cancellationToken)));
    }

    public async Task<bool> SetEmployeeStatusAsync(Guid employeeId, bool isActive, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(item => item.EmployeeId == employeeId, cancellationToken);
        if (employee is null) return false;
        employee.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<Employee> EmployeeGraph() => db.Employees.AsSplitQuery()
        .Include(item => item.Gender).Include(item => item.MaritalStatus).Include(item => item.Nationality)
        .Include(item => item.Contacts)
        .Include(item => item.Addresses).ThenInclude(item => item.AddressType)
        .Include(item => item.Addresses).ThenInclude(item => item.Country)
        .Include(item => item.EmergencyContacts)
        .Include(item => item.EmploymentRecords.Where(record => record.IsCurrent)).ThenInclude(item => item.Department)
        .Include(item => item.EmploymentRecords.Where(record => record.IsCurrent)).ThenInclude(item => item.Designation)
        .Include(item => item.EmploymentRecords.Where(record => record.IsCurrent)).ThenInclude(item => item.Location)
        .Include(item => item.EmploymentRecords.Where(record => record.IsCurrent)).ThenInclude(item => item.EmploymentType)
        .Include(item => item.EmploymentRecords.Where(record => record.IsCurrent)).ThenInclude(item => item.EmploymentStatus)
        .Include(item => item.EmploymentRecords.Where(record => record.IsCurrent)).ThenInclude(item => item.HiringSource)
        .Include(item => item.EmploymentRecords.Where(record => record.IsCurrent)).ThenInclude(item => item.ReportingToEmployee)
        .Include(item => item.TeacherProfile)
        .Include(item => item.Compensations.Where(compensation => compensation.IsCurrent)).ThenInclude(item => item.PayType);

    private static EmployeeDetailDto Map(Employee employee)
    {
        var current = employee.EmploymentRecords.SingleOrDefault(item => item.IsCurrent);
        var compensation = employee.Compensations.SingleOrDefault(item => item.IsCurrent);
        return new EmployeeDetailDto
        {
            EmployeeId = employee.EmployeeId, EmployeeNumber = employee.EmployeeNumber, FirstName = employee.FirstName,
            MiddleName = employee.MiddleName, LastName = employee.LastName, PreferredName = employee.PreferredName,
            DateOfBirth = employee.DateOfBirth, Gender = employee.Gender?.Name, MaritalStatus = employee.MaritalStatus?.Name,
            Nationality = employee.Nationality?.Name, ProfilePhoto = employee.ProfilePhoto, IsActive = employee.IsActive,
            CreatedAt = employee.CreatedAt, UpdatedAt = employee.UpdatedAt,
            Contacts = employee.Contacts.Select(item => new EmployeeContactDto
            {
                EmployeeContactId = item.EmployeeContactId, WorkEmail = item.WorkEmail, PersonalEmail = item.PersonalEmail,
                Mobile = item.Mobile, Phone = item.Phone, WorkPhone = item.WorkPhone, IsPrimary = item.IsPrimary
            }).ToList(),
            Addresses = employee.Addresses.Select(item => new EmployeeAddressDto
            {
                EmployeeAddressId = item.EmployeeAddressId, AddressTypeId = item.AddressTypeId, AddressType = item.AddressType.Name,
                AddressLine1 = item.AddressLine1, AddressLine2 = item.AddressLine2, City = item.City,
                StateProvince = item.StateProvince, CountryId = item.CountryId, Country = item.Country?.Name,
                PostalCode = item.PostalCode, IsPrimary = item.IsPrimary
            }).ToList(),
            EmergencyContacts = employee.EmergencyContacts.Select(item => new EmergencyContactDto
            {
                EmergencyContactId = item.EmergencyContactId, Name = item.Name, Relationship = item.Relationship,
                Mobile = item.Mobile, Phone = item.Phone, Email = item.Email, IsPrimary = item.IsPrimary
            }).ToList(),
            CurrentEmployment = current is null ? null : new EmploymentSummaryDto
            {
                EmploymentRecordId = current.EmploymentRecordId, DepartmentId = current.DepartmentId, Department = current.Department?.Name,
                DesignationId = current.DesignationId, Designation = current.Designation?.Name, LocationId = current.LocationId,
                Location = current.Location?.Name, EmploymentTypeId = current.EmploymentTypeId, EmploymentType = current.EmploymentType?.Name,
                EmploymentStatusId = current.EmploymentStatusId, EmploymentStatus = current.EmploymentStatus?.Name,
                HiringSourceId = current.HiringSourceId, HiringSource = current.HiringSource?.Name,
                ReportingToEmployeeId = current.ReportingToEmployeeId,
                ReportingToEmployeeNumber = current.ReportingToEmployee?.EmployeeNumber,
                ReportingToName = current.ReportingToEmployee is null ? null : $"{current.ReportingToEmployee.FirstName} {current.ReportingToEmployee.LastName}",
                HireDate = current.HireDate, StartDate = current.StartDate, EndDate = current.EndDate, IsCurrent = current.IsCurrent
            },
            TeacherProfile = employee.TeacherProfile is null ? null : new TeacherProfileDto
            {
                TeacherProfileId = employee.TeacherProfile.TeacherProfileId, TeacherCode = employee.TeacherProfile.TeacherCode,
                TeachingLevel = employee.TeacherProfile.TeachingLevel, Specialization = employee.TeacherProfile.Specialization,
                YearsOfExperience = employee.TeacherProfile.YearsOfExperience, TeachingStatus = employee.TeacherProfile.TeachingStatus
            },
            CurrentCompensation = compensation is null ? null : new CompensationSummaryDto
            {
                EmployeeCompensationId = compensation.EmployeeCompensationId, PayTypeId = compensation.PayTypeId,
                PayType = compensation.PayType?.Name, BasicSalary = compensation.BasicSalary, Currency = compensation.Currency,
                EffectiveFrom = compensation.EffectiveFrom, EffectiveTo = compensation.EffectiveTo
            }
        };
    }

    private async Task<ServiceResult<EmployeeDetailDto>?> ValidateWriteRequest(EmployeeWriteRequest request, Guid? employeeId, CancellationToken ct)
    {
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true))
            return Invalid(string.Join(" ", validationResults.Select(item => item.ErrorMessage).Where(message => !string.IsNullOrWhiteSpace(message))));
        if (request.Contacts?.Any(item => !IsValid(item)) == true
            || request.Addresses?.Any(item => !IsValid(item)) == true
            || request.EmergencyContacts?.Any(item => !IsValid(item)) == true
            || request.TeacherProfile is not null && !IsValid(request.TeacherProfile))
            return Invalid("One or more contact, address, emergency-contact, or teacher-profile fields are invalid.");
        if (string.IsNullOrWhiteSpace(request.EmployeeNumber) || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return Invalid("EmployeeNumber, FirstName, and LastName are required.");
        if (request.DepartmentId is null || request.DesignationId is null || request.EmploymentTypeId is null
            || request.EmploymentStatusId is null || request.HireDate is null)
            return Invalid("DepartmentId, DesignationId, EmploymentTypeId, EmploymentStatusId, and HireDate are required.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth > today) return Invalid("DateOfBirth cannot be in the future.");
        if (request.HireDate > today.AddYears(1)) return Invalid("HireDate is outside the supported date range.");
        if (request.EndDate.HasValue && request.StartDate.HasValue && request.EndDate < request.StartDate)
            return Invalid("EndDate cannot be earlier than StartDate.");
        if (request.EndDate.HasValue && request.EndDate < request.HireDate)
            return Invalid("EndDate cannot be earlier than HireDate.");

        if (!await Active<Department>(request.DepartmentId!.Value, ct)) return Invalid("DepartmentId must reference an active department.");
        if (!await Active<Designation>(request.DesignationId!.Value, ct)) return Invalid("DesignationId must reference an active designation.");
        if (!await Active<EmploymentType>(request.EmploymentTypeId!.Value, ct)) return Invalid("EmploymentTypeId must reference an active employment type.");
        if (!await Active<EmploymentStatus>(request.EmploymentStatusId!.Value, ct)) return Invalid("EmploymentStatusId must reference an active employment status.");
        if (!await OptionalActive<Gender>(request.GenderId, ct) || !await OptionalActive<MaritalStatus>(request.MaritalStatusId, ct)
            || !await OptionalActive<Nationality>(request.NationalityId, ct) || !await OptionalActive<Location>(request.LocationId, ct)
            || !await OptionalActive<HiringSource>(request.HiringSourceId, ct)) return Invalid("A supplied master-data ID does not reference an active record.");
        if (employeeId.HasValue && request.ReportingToEmployeeId == employeeId)
            return Invalid("An employee cannot report to themselves.");
        if (request.ReportingToEmployeeId is Guid managerId && !await db.Employees.AnyAsync(item => item.EmployeeId == managerId, ct))
            return Invalid("ReportingToEmployeeId does not reference an existing employee.");
        if (request.Addresses is not null)
        {
            if (request.Addresses.Count(item => item.IsPrimary) > 1) return Invalid("Only one address may be primary.");
            foreach (var address in request.Addresses)
                if (address.AddressTypeId is null || !await Active<AddressType>(address.AddressTypeId.Value, ct) || !await OptionalActive<Country>(address.CountryId, ct))
                    return Invalid("Each address must reference an active address type and, when supplied, an active country.");
        }
        if (request.Contacts is not null && request.Contacts.Count(item => item.IsPrimary) > 1)
            return Invalid("Only one contact may be primary.");
        if (request.EmergencyContacts is not null && request.EmergencyContacts.Count(item => item.IsPrimary) > 1)
            return Invalid("Only one emergency contact may be primary.");
        if (request.TeacherProfile is not null && await db.TeacherProfiles.AnyAsync(item => item.TeacherCode == request.TeacherProfile.TeacherCode && item.EmployeeId != employeeId, ct))
            return ServiceResult<EmployeeDetailDto>.Fail("conflict", "TeacherCode is already in use.");
        return null;
    }

    private Task<bool> Active<T>(Guid id, CancellationToken ct) where T : MasterDataEntity => db.Set<T>().AnyAsync(item => item.Id == id && item.IsActive, ct);
    private Task<bool> OptionalActive<T>(Guid? id, CancellationToken ct) where T : MasterDataEntity => id is null ? Task.FromResult(true) : Active<T>(id.Value, ct);
    private static ServiceResult<EmployeeDetailDto> Invalid(string message) => ServiceResult<EmployeeDetailDto>.Fail("validation", message);

    private static bool IsValid(object value) => Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), validateAllProperties: true);

    private static EmploymentRecord CreateEmployment(EmployeeWriteRequest request) => new()
    {
        DepartmentId = request.DepartmentId, DesignationId = request.DesignationId, LocationId = request.LocationId,
        EmploymentTypeId = request.EmploymentTypeId, EmploymentStatusId = request.EmploymentStatusId,
        HiringSourceId = request.HiringSourceId, ReportingToEmployeeId = request.ReportingToEmployeeId,
        HireDate = request.HireDate!.Value, StartDate = request.StartDate, EndDate = request.EndDate, IsCurrent = true
    };

    private static void UpdateEmployment(EmploymentRecord record, EmployeeWriteRequest request)
    {
        record.DepartmentId = request.DepartmentId; record.DesignationId = request.DesignationId; record.LocationId = request.LocationId;
        record.EmploymentTypeId = request.EmploymentTypeId; record.EmploymentStatusId = request.EmploymentStatusId;
        record.HiringSourceId = request.HiringSourceId;
        if (request.ReportingToEmployeeId.HasValue) record.ReportingToEmployeeId = request.ReportingToEmployeeId;
        record.HireDate = request.HireDate!.Value; record.StartDate = request.StartDate; record.EndDate = request.EndDate;
    }

    private static void AddChildren(Employee employee, EmployeeWriteRequest request)
    {
        if (request.Contacts is not null) foreach (var item in request.Contacts) employee.Contacts.Add(ToContact(item));
        if (request.Addresses is not null) foreach (var item in request.Addresses) employee.Addresses.Add(ToAddress(item));
        if (request.EmergencyContacts is not null) foreach (var item in request.EmergencyContacts) employee.EmergencyContacts.Add(ToEmergencyContact(item));
        if (request.TeacherProfile is not null) employee.TeacherProfile = ToTeacherProfile(request.TeacherProfile);
    }

    private static EmployeeContact ToContact(EmployeeContactRequest item) => new()
    {
        WorkEmail = Clean(item.WorkEmail), PersonalEmail = Clean(item.PersonalEmail), Mobile = Clean(item.Mobile),
        Phone = Clean(item.Phone), WorkPhone = Clean(item.WorkPhone), IsPrimary = item.IsPrimary
    };
    private static EmployeeAddress ToAddress(EmployeeAddressRequest item) => new()
    {
        AddressTypeId = item.AddressTypeId!.Value, AddressLine1 = item.AddressLine1.Trim(), AddressLine2 = Clean(item.AddressLine2),
        City = Clean(item.City), StateProvince = Clean(item.StateProvince), CountryId = item.CountryId,
        PostalCode = Clean(item.PostalCode), IsPrimary = item.IsPrimary
    };
    private static EmergencyContact ToEmergencyContact(EmergencyContactRequest item) => new()
    {
        Name = item.Name.Trim(), Relationship = item.Relationship.Trim(), Mobile = Clean(item.Mobile), Phone = Clean(item.Phone),
        Email = Clean(item.Email), IsPrimary = item.IsPrimary
    };
    private static TeacherProfile ToTeacherProfile(TeacherProfileRequest item) => new()
    {
        TeacherCode = item.TeacherCode.Trim(), TeachingLevel = Clean(item.TeachingLevel), Specialization = Clean(item.Specialization),
        YearsOfExperience = item.YearsOfExperience, TeachingStatus = item.TeachingStatus.Trim()
    };
    private static void UpdateTeacherProfile(TeacherProfile profile, TeacherProfileRequest item)
    {
        profile.TeacherCode = item.TeacherCode.Trim(); profile.TeachingLevel = Clean(item.TeachingLevel);
        profile.Specialization = Clean(item.Specialization); profile.YearsOfExperience = item.YearsOfExperience;
        profile.TeachingStatus = item.TeachingStatus.Trim();
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsEmployeeNumberConflict(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && (sql.Number is 2601 or 2627)
                && (sql.Message.Contains("EmployeeNumber", StringComparison.OrdinalIgnoreCase)
                    || sql.Message.Contains("IX_Employees_EmployeeNumber", StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }
}
