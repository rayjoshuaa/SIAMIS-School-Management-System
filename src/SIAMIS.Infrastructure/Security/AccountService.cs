using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Security;

public sealed class AccountService(SIAMISDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signin, ICurrentActor actor, IEmployeeAccountLifecycleService lifecycle) : IAccountService
{
    private static ServiceResult<SecurityUserDto> Fail(string code, string message) => ServiceResult<SecurityUserDto>.Fail(code, message);
    private async Task<SecurityUserDto> Dto(ApplicationUser u)
    {
        var roles = await users.GetRolesAsync(u);
        var readiness = u.EmployeeId.HasValue ? (await lifecycle.GetAsync(u.EmployeeId.Value, CancellationToken.None)).Value : null;
        return new(u.Id, u.UserName!, u.Email, u.EmployeeId, u.IsActive, u.RequiresPasswordChange, u.AdministrationVersion, roles.ToArray(), u.RequiresPasswordChange ? [] : SecurityCapabilities.ForRoles(roles))
        { CurrentEmploymentRecordId = readiness?.CurrentEmploymentRecordId, CurrentEmploymentStatus = readiness?.CurrentEmploymentStatus,
            HasCurrentEmployment = readiness?.HasCurrentEmployment, RequiresOffboardingDecision = readiness?.RequiresOffboardingDecision ?? false };
    }
    private async Task Audit(string operation, Guid? target = null)
    {
        db.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, Operation = operation, ResourceType = "User", ResourceId = target?.ToString() });
        await db.SaveChangesAsync();
    }
    public async Task<bool> LoginAsync(LoginRequest r)
    {
        var u = await users.FindByNameAsync(r.UserName);
        // One generic response for missing, disabled, locked and invalid accounts.
        bool ok = u is not null && u.IsActive && (await signin.CheckPasswordSignInAsync(u, r.Password, true)).Succeeded;
        if (ok)
        {
            await signin.SignInAsync(u!, false);
            db.Add(new SecurityAuditEvent { ActorUserId = u!.Id, Operation = "LoginSucceeded", ResourceType = "User", ResourceId = u.Id.ToString() });
            await db.SaveChangesAsync();
        }
        else await Audit("LoginFailed");
        return ok;
    }
    public async Task LogoutAsync() { await Audit("Logout", actor.UserId); await signin.SignOutAsync(); }
    public async Task<SecurityUserDto?> MeAsync() => actor.UserId is Guid id ? (await GetAsync(id)).Value : null;
    public async Task<ServiceResult<SecurityUserDto>> GetAsync(Guid id)
    {
        var u = await users.FindByIdAsync(id.ToString());
        return u is null ? Fail("not_found", "User was not found.") : ServiceResult<SecurityUserDto>.Success(await Dto(u));
    }
    public async Task<PagedResult<SecurityUserDto>> ListAsync(int page, int size, CancellationToken ct)
    {
        var list = await db.Users.AsNoTracking().OrderBy(u => u.NormalizedUserName).ThenBy(u => u.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var result = new List<SecurityUserDto>(); foreach (var u in list) result.Add(await Dto(u));
        return new(result, page, size, await db.Users.CountAsync(ct));
    }
    public async Task<ServiceResult<SecurityUserDto>> CreateAsync(CreateUserRequest r, CancellationToken ct)
    {
        if (r.Roles.Any(x => !SecurityCapabilities.Roles.ContainsKey(x)) || r.EmployeeId == Guid.Empty)
            return Fail("validation", "Valid system roles and employee linkage are required.");
        if (r.Roles.Contains("Employee") && !r.EmployeeId.HasValue) return Fail("validation", "Employee role requires an Employee linkage.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (r.EmployeeId.HasValue && await SIAMIS.Infrastructure.Services.EmploymentIntegrity.LockAsync(db, r.EmployeeId.Value, ct) is null) return Fail("not_found", "Employee was not found.");
            var u = new ApplicationUser { Id = Guid.NewGuid(), UserName = r.UserName.Trim(), Email = r.Email?.Trim(), EmployeeId = r.EmployeeId, LockoutEnabled = true };
            var result = await users.CreateAsync(u, r.TemporaryPassword);
            if (!result.Succeeded) return Fail(result.Errors.Any(e=>e.Code.StartsWith("Duplicate",StringComparison.Ordinal))?"conflict":"validation", string.Join(" ", result.Errors.Select(e => e.Description)));
            result = await users.AddToRolesAsync(u, r.Roles.Distinct());
            if (!result.Succeeded) return Fail("conflict", "Account roles could not be assigned.");
            await Audit("AccountCreated", u.Id); await tx.CommitAsync(ct);
            return ServiceResult<SecurityUserDto>.Success(await Dto(u));
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 }) { db.ChangeTracker.Clear(); return Fail("conflict", "Username, email or employee linkage is already assigned."); }
    }
    public Task<ServiceResult<SecurityUserDto>> StatusAsync(Guid id, UserStatusRequest r, CancellationToken ct)
        => Mutate(id, r.Version, r.IsActive, null, ct);
    public Task<ServiceResult<SecurityUserDto>> RolesAsync(Guid id, UserRolesRequest r, CancellationToken ct)
        => Mutate(id, r.Version, null, r.Roles, ct);
    private async Task<ServiceResult<SecurityUserDto>> Mutate(Guid id, string version, bool? active, string[]? roles, CancellationToken ct)
    {
        try { return await MutateLocked(id, version, active, roles, ct); }
        catch (Exception e) when (e is SqlException { Number: 1205 } || e is DbUpdateConcurrencyException || e is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } })
        { return Fail("conflict", "Concurrent account or employment state changed. Reload before retrying."); }
    }
    private async Task<ServiceResult<SecurityUserDto>> MutateLocked(Guid id, string version, bool? active, string[]? roles, CancellationToken ct)
    {
        if (roles?.Any(r => !SecurityCapabilities.Roles.ContainsKey(r)) == true) return Fail("validation", "Unknown role.");
        // Linkage is immutable through administration; read it before opening the locking transaction.
        var linkedEmployee = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.EmployeeId).SingleOrDefaultAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var u = await AccountLock.LockAsync(db, id, linkedEmployee, ct);
        if (u is null) return Fail("not_found", "User was not found.");
        if (u.AdministrationVersion != version) return Fail("conflict", "User changed. Reload before modifying.");
        if (roles?.Contains("Employee") == true && !u.EmployeeId.HasValue) return Fail("validation", "Employee role requires a linked employee.");
        IdentityResult result;
        if (roles is not null)
        {
            var previous = await users.GetRolesAsync(u);
            result = await users.RemoveFromRolesAsync(u, previous.Except(roles));
            if (!result.Succeeded) return Fail("conflict", "Concurrent role change.");
            result = await users.AddToRolesAsync(u, roles.Except(previous));
            if (!result.Succeeded) return Fail("conflict", "Concurrent role change.");
        }
        if (active.HasValue) u.IsActive = active.Value;
        u.AdministrationVersion = Guid.NewGuid().ToString();
        result = await users.UpdateSecurityStampAsync(u);
        if (!result.Succeeded) return Fail("conflict", "Concurrent user change.");
        await Audit(roles is not null ? "RolesChanged" : u.IsActive ? "AccountEnabled" : "AccountDisabled", id); await tx.CommitAsync(ct);
        return ServiceResult<SecurityUserDto>.Success(await Dto(u));
    }
    public async Task<bool> ChangePasswordAsync(ChangePasswordRequest r)
    {
        var u = actor.UserId is Guid id ? await users.FindByIdAsync(id.ToString()) : null;
        if (u is null || !u.IsActive) return false;
        var result = await users.ChangePasswordAsync(u, r.CurrentPassword, r.NewPassword);
        if (!result.Succeeded) return false;
        u.RequiresPasswordChange = false; u.AdministrationVersion = Guid.NewGuid().ToString();
        if (!(await users.UpdateAsync(u)).Succeeded) return false;
        await Audit("PasswordChanged", u.Id); await signin.RefreshSignInAsync(u); return true;
    }
}
