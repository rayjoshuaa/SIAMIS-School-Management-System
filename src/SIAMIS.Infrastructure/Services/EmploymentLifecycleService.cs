using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmploymentLifecycleService(SIAMISDbContext db, IEmployeeAccountLifecycleService accounts) : IEmploymentLifecycleService, IEmploymentResolver
{
    public Task<ServiceResult<EmploymentRecordDto>> ChangeAsync(Guid employeeId, EmploymentChangeRequest request, CancellationToken ct)
        => MutateAsync(employeeId, request, "change", request.EffectiveDate, ct);
    public Task<ServiceResult<EmploymentRecordDto>> EndAsync(Guid employeeId, EndEmploymentRequest request, CancellationToken ct)
        => MutateAsync(employeeId, request, "end", request.EndDate, ct);
    public Task<ServiceResult<EmploymentRecordDto>> RehireAsync(Guid employeeId, RehireRequest request, CancellationToken ct)
        => MutateAsync(employeeId, request, "rehire", request.StartDate ?? request.HireDate, ct);

    private async Task<ServiceResult<EmploymentRecordDto>> MutateAsync(Guid id, object request, string action, DateOnly? date, CancellationToken ct)
    {
        try { return await MutateLockedAsync(id, request, action, date, ct); }
        catch (Exception e) when (e is SqlException { Number: 1205 } || e is DbUpdateConcurrencyException || e is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } })
        { return Fail("conflict", "Concurrent employment or account state changed. Reload before retrying."); }
    }

    private async Task<ServiceResult<EmploymentRecordDto>> MutateLockedAsync(Guid id, object request, string action, DateOnly? date, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(request, new ValidationContext(request), [], true) || !date.HasValue)
            return Fail("validation", "Required lifecycle date and context fields must be supplied.");
        if (date.Value > DateOnly.FromDateTime(DateTime.UtcNow)) return Fail("validation", "Future-dated lifecycle actions are not supported.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var employee = await EmploymentIntegrity.LockAsync(db, id, ct);
        if (employee is null) return Fail("not_found", "Employee was not found.");
        var records = await db.EmploymentRecords.Where(x => x.EmployeeId == id).ToListAsync(ct);
        var currents = records.Where(x => x.IsCurrent).ToList();
        if (currents.Count > 1 || records.Any(x => EmploymentIntegrity.Dates(x) is not null))
            return Fail("conflict", "Existing employment history is inconsistent; review it before lifecycle changes.");
        for (var i = 0; i < records.Count; i++)
            for (var j = i + 1; j < records.Count; j++)
                if (EmploymentIntegrity.Overlaps(records[i], records[j])) return Fail("conflict", "Existing employment ranges overlap.");
        var current = currents.SingleOrDefault();
        EmploymentRecord result;
        if (action == "rehire")
        {
            if (current is not null || employee.IsActive || records.Any(x => x.EndDate is null))
                return Fail("conflict", "Rehire requires an inactive employee with no current/open employment.");
            var r = (RehireRequest)request;
            result = new EmploymentRecord { EmployeeId = id, HireDate = r.HireDate!.Value, StartDate = r.StartDate,
                DepartmentId = r.DepartmentId, DesignationId = r.DesignationId, LocationId = r.LocationId,
                EmploymentTypeId = r.EmploymentTypeId, EmploymentStatusId = r.EmploymentStatusId,
                ReportingToEmployeeId = r.ReportingToEmployeeId, HiringSourceId = r.HiringSourceId, IsCurrent = true };
            if (records.Any(x => x.EndDate >= EmploymentIntegrity.Start(result)))
                return Fail("validation", "Rehire must start after all previous employment intervals.");
        }
        else
        {
            if (current is null || current.EndDate.HasValue) return Fail("conflict", "There is no current open employment.");
            if (action == "end")
            {
                var r = (EndEmploymentRequest)request;
                if (r.ExpectedEmploymentRecordId != current.EmploymentRecordId) return Fail("conflict", "Current employment changed. Reload employment history before ending it.");
                if (!await db.EmploymentStatuses.AnyAsync(x => x.Id == r.EmploymentStatusId && x.IsActive && x.IsTerminal, ct))
                    return Fail("validation", "End employment requires an active terminal EmploymentStatus.");
                if (date.Value < EmploymentIntegrity.Start(current)) return Fail("validation", "EndDate cannot be before EmploymentStart.");
                var accountError = await accounts.ResolveEndAsync(id, current.EmploymentRecordId, r, ct);
                if (accountError is not null) return Fail(accountError.Code, accountError.Message);
                current.EndDate = date; current.IsCurrent = false; current.EmploymentStatusId = r.EmploymentStatusId;
                employee.IsActive = false; result = current;
            }
            else
            {
                if (!employee.IsActive) return Fail("conflict", "Inactive employee with open employment requires legacy-state review or end-employment.");
                if (date.Value <= EmploymentIntegrity.Start(current)) return Fail("validation", "EffectiveDate must be after the current EmploymentStart.");
                var r = (EmploymentChangeRequest)request;
                result = new EmploymentRecord { EmployeeId = id, HireDate = current.HireDate, StartDate = date,
                    DepartmentId = r.DepartmentId ?? current.DepartmentId, DesignationId = r.DesignationId ?? current.DesignationId,
                    LocationId = r.LocationId ?? current.LocationId, EmploymentTypeId = r.EmploymentTypeId ?? current.EmploymentTypeId,
                    EmploymentStatusId = r.EmploymentStatusId ?? current.EmploymentStatusId,
                    ReportingToEmployeeId = r.ReportingToEmployeeId ?? current.ReportingToEmployeeId,
                    HiringSourceId = r.HiringSourceId ?? current.HiringSourceId, IsCurrent = true };
                current.EndDate = date.Value.AddDays(-1); current.IsCurrent = false;
            }
        }
        var error = EmploymentIntegrity.Dates(result);
        if (error is not null) return Fail("validation", error);
        if (action != "end")
        {
            error = await EmploymentIntegrity.ContextAsync(db, result, ct);
            if (error is not null) return Fail("validation", error);
            if (records.Any(x => EmploymentIntegrity.Overlaps(x, result))) return Fail("validation", "Employment ranges cannot overlap (EndDate is inclusive).");
            // Release the filtered unique current slot before inserting the replacement.
            await db.SaveChangesAsync(ct);
            db.EmploymentRecords.Add(result); employee.IsActive = true;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<EmploymentRecordDto>.Success(Map(result));
    }

    public async Task<ServiceResult<IReadOnlyList<EmploymentRecordDto>>> HistoryAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.EmployeeId == id, ct)) return ServiceResult<IReadOnlyList<EmploymentRecordDto>>.Fail("not_found", "Employee was not found.");
        return ServiceResult<IReadOnlyList<EmploymentRecordDto>>.Success(await Project(Records(id).OrderBy(x => x.StartDate ?? x.HireDate).ThenBy(x => x.EmploymentRecordId)).ToListAsync(ct));
    }

    public async Task<ServiceResult<EmploymentRecordDto?>> ResolveAsync(Guid id, DateOnly date, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(x => x.EmployeeId == id, ct)) return ServiceResult<EmploymentRecordDto?>.Fail("not_found", "Employee was not found.");
        var matches = await Project(Records(id).Where(EmploymentIntegrity.EffectiveOn(date))
            .OrderBy(x => x.EmploymentRecordId).Take(2)).ToListAsync(ct);
        return matches.Count > 1 ? ServiceResult<EmploymentRecordDto?>.Fail("conflict", "Multiple employment records match the supplied date; history is inconsistent.")
            : ServiceResult<EmploymentRecordDto?>.Success(matches.SingleOrDefault());
    }

    private IQueryable<EmploymentRecord> Records(Guid id) => db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == id);
    private static IQueryable<EmploymentRecordDto> Project(IQueryable<EmploymentRecord> query)
        => query.Select(x => new EmploymentRecordDto(x.EmploymentRecordId, x.EmployeeId, x.DepartmentId, x.DesignationId, x.LocationId,
            x.EmploymentTypeId, x.EmploymentStatusId, x.ReportingToEmployeeId, x.HiringSourceId, x.HireDate, x.StartDate, x.EndDate, x.IsCurrent));
    private static EmploymentRecordDto Map(EmploymentRecord x) => new(x.EmploymentRecordId, x.EmployeeId, x.DepartmentId, x.DesignationId,
        x.LocationId, x.EmploymentTypeId, x.EmploymentStatusId, x.ReportingToEmployeeId, x.HiringSourceId, x.HireDate, x.StartDate, x.EndDate, x.IsCurrent);
    private static ServiceResult<EmploymentRecordDto> Fail(string code, string message) => ServiceResult<EmploymentRecordDto>.Fail(code, message);
}
