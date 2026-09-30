using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class PayrollEmploymentContextService(SIAMISDbContext db) : IPayrollEmploymentContextService
{
    public async Task<IReadOnlyDictionary<Guid, ServiceResult<PayrollEmploymentContextDto?>>> ResolveAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().ToArray();
        var result = ids.ToDictionary(id => id, _ => ServiceResult<PayrollEmploymentContextDto?>.Success(null));
        if (ids.Length == 0) return result;
        var rows = await db.EmploymentRecords.AsNoTracking().Where(x => ids.Contains(x.EmployeeId))
            .Where(EmploymentIntegrity.Overlapping(periodStart, periodEnd))
            .Select(x => new
            {
                x.EmployeeId, x.EmploymentRecordId, x.HireDate, x.StartDate, x.EndDate,
                x.DepartmentId, DepartmentName = x.Department == null ? null : x.Department.Name,
                x.DesignationId, DesignationName = x.Designation == null ? null : x.Designation.Name,
                x.EmploymentTypeId, EmploymentTypeName = x.EmploymentType == null ? null : x.EmploymentType.Name,
                x.LocationId, LocationName = x.Location == null ? null : x.Location.Name
            }).ToListAsync(ct);
        foreach (var group in rows.GroupBy(x => x.EmployeeId))
        {
            var ordered = group.OrderBy(x => x.StartDate ?? x.HireDate).ThenBy(x => x.EmploymentRecordId).ToArray();
            // Valid intervals may be adjacent or have gaps. Overlapping intervals are corrupt,
            // including ties: the ID ordering must never silently choose a target context.
            var corrupt = ordered.Any(x => x.StartDate < x.HireDate || x.EndDate < (x.StartDate ?? x.HireDate));
            for (var i = 1; i < ordered.Length; i++)
                if (!ordered[i - 1].EndDate.HasValue || ordered[i - 1].EndDate >= (ordered[i].StartDate ?? ordered[i].HireDate)) corrupt = true;
            if (corrupt)
            {
                result[group.Key] = ServiceResult<PayrollEmploymentContextDto?>.Fail("conflict", "Employment history has overlapping or invalid intervals in this payroll period; target context cannot be resolved safely.");
                continue;
            }
            // The earliest overlapping record either covers periodStart or begins at
            // the employee's first eligible point within the period.
            var selected = ordered[0];
            var start = selected.StartDate ?? selected.HireDate;
            var contextDate = start > periodStart ? start : periodStart;
            result[group.Key] = ServiceResult<PayrollEmploymentContextDto?>.Success(new(selected.EmploymentRecordId, contextDate,
                selected.DepartmentId, selected.DepartmentName, selected.DesignationId, selected.DesignationName,
                selected.EmploymentTypeId, selected.EmploymentTypeName, selected.LocationId, selected.LocationName));
        }
        return result;
    }
}
