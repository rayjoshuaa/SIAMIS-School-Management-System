using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Security;

public sealed class ResourceAccessService(SIAMISDbContext db) : IResourceAccessService
{
    public Task<bool> OwnFinalPayrollAsync(Guid payrollId, Guid employeeId, CancellationToken ct)
        => db.EmployeePayrolls.AsNoTracking().AnyAsync(p => p.EmployeePayrollId == payrollId && p.EmployeeId == employeeId && (p.Status == "Approved" || p.Status == "Paid"), ct);
}
