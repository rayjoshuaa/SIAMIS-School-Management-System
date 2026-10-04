using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Security;

/// <summary>Employee -> shared role gate -> User. Credential/privilege writers share this gate.</summary>
internal static class SystemAdminGuard
{
    public static async Task LockAsync(SIAMISDbContext db, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Admin guard requires a transaction.");
        await db.Roles.FromSqlRaw("SELECT * FROM [Roles] WITH (UPDLOCK) WHERE [NormalizedName] = 'SYSTEMADMIN'").AsNoTracking().SingleAsync(ct);
    }
    public static async Task<ApiFailure?> CanRemoveAsync(SIAMISDbContext db, ApplicationUser target, CancellationToken ct)
    {
        if (!target.IsActive || !await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == target.Id && r.NormalizedName == "SYSTEMADMIN" select ur).AnyAsync(ct)) return null;
        var now = DateTimeOffset.UtcNow;
        bool other = await (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join r in db.Roles on ur.RoleId equals r.Id
            where u.Id != target.Id && r.NormalizedName == "SYSTEMADMIN" && u.IsActive && u.PasswordHash != null && !u.RequiresPasswordChange
                && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now) select u.Id).AnyAsync(ct);
        return other ? null : new("last_usable_system_admin_required", "At least one other usable SystemAdmin must remain.");
    }
}
