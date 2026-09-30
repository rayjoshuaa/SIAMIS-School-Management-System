using Microsoft.EntityFrameworkCore;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

// Call only inside a transaction. Mutations lock the period before payrolls so
// finalization and child changes cannot validate conflicting views of the same run.
internal static class PayrollPeriodLock
{
    public static Task<PayrollPeriod?> GetAsync(SIAMISDbContext db, Guid id, CancellationToken ct)
        => db.PayrollPeriods.FromSqlInterpolated(
            $"SELECT * FROM [PayrollPeriods] WITH (UPDLOCK) WHERE [PayrollPeriodId] = {id}")
            .SingleOrDefaultAsync(ct);

    public static bool AllowsMutation(string status) => status is "Open" or "Processing";
    public static string ConflictMessage(string status)
        => $"Parent payroll period is {status}; payroll mutations are allowed only in Open or Processing periods.";
}
