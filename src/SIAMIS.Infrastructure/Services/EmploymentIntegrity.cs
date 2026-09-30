using Microsoft.EntityFrameworkCore;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

internal static class EmploymentIntegrity
{
    // All existing-employee employment writers acquire this first inside Serializable.
    public static Task<Employee?> LockAsync(SIAMISDbContext db, Guid id, CancellationToken ct)
        => db.Employees.FromSqlInterpolated($"SELECT * FROM [Employees] WITH (UPDLOCK) WHERE [EmployeeId] = {id}").SingleOrDefaultAsync(ct);

    public static DateOnly Start(EmploymentRecord r) => r.StartDate ?? r.HireDate;
    public static string? Dates(EmploymentRecord r)
        => r.StartDate < r.HireDate ? "StartDate cannot be before HireDate."
        : r.EndDate < Start(r) ? "EndDate cannot be before EmploymentStart."
        : r.IsCurrent && r.EndDate.HasValue ? "Current employment must be open-ended; use end-employment."
        : null;

    public static bool Overlaps(EmploymentRecord a, EmploymentRecord b)
        => (!b.EndDate.HasValue || Start(a) <= b.EndDate.Value) && (!a.EndDate.HasValue || Start(b) <= a.EndDate.Value);

    public static async Task<string?> ContextAsync(SIAMISDbContext db, EmploymentRecord r, CancellationToken ct)
    {
        async Task<bool> Active<T>(Guid? id, bool required = false) where T : MasterDataEntity
            => id.HasValue ? await db.Set<T>().AnyAsync(x => x.Id == id && x.IsActive, ct) : !required;
        if (!await Active<Department>(r.DepartmentId, true) || !await Active<Designation>(r.DesignationId, true)
            || !await Active<EmploymentType>(r.EmploymentTypeId, true) || !await Active<Location>(r.LocationId)
            || !await Active<HiringSource>(r.HiringSourceId)) return "Employment context must reference active master data.";
        if (!await db.EmploymentStatuses.AnyAsync(x => x.Id == r.EmploymentStatusId && x.IsActive && !x.IsTerminal, ct))
            return "Open employment requires an active non-terminal EmploymentStatus.";
        if (r.ReportingToEmployeeId == r.EmployeeId) return "An employee cannot report to themselves.";
        if (r.ReportingToEmployeeId.HasValue && !await db.Employees.AnyAsync(x => x.EmployeeId == r.ReportingToEmployeeId, ct))
            return "ReportingToEmployeeId does not reference an existing employee.";
        return null;
    }

}
