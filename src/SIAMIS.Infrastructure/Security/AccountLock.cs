using Microsoft.EntityFrameworkCore;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Services;

namespace SIAMIS.Infrastructure.Security;

/// <summary>Linked account writers use Employee then User, within the caller's transaction.</summary>
internal static class AccountLock
{
    public static async Task<ApplicationUser?> LockAsync(SIAMISDbContext db, Guid id, Guid? employee, CancellationToken ct)
    {
        if (employee.HasValue) await EmploymentIntegrity.LockAsync(db, employee.Value, ct);
        var fresh = await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK) WHERE [Id] = {id}").AsNoTracking().SingleOrDefaultAsync(ct);
        if (fresh is null) return null;
        if (fresh.EmployeeId != employee) throw new DbUpdateConcurrencyException("Account linkage changed.");
        // Cookie validation can already track the actor; never validate versions against that stale instance.
        var tracked = db.Users.Local.SingleOrDefault(u => u.Id == id);
        if (tracked is null) { db.Attach(fresh); return fresh; }
        db.Entry(tracked).CurrentValues.SetValues(fresh);
        db.Entry(tracked).OriginalValues.SetValues(fresh);
        return tracked;
    }
}
