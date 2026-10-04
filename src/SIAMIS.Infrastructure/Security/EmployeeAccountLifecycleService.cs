using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Security;

public sealed class EmployeeAccountLifecycleService(SIAMISDbContext db, UserManager<ApplicationUser> users, ICurrentActor actor) : IEmployeeAccountLifecycleService
{
    public async Task<ServiceResult<EmployeeAccountLifecycleDto>> GetAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(e => e.EmployeeId == employeeId, ct))
            return ServiceResult<EmployeeAccountLifecycleDto>.Fail("not_found", "Employee was not found.");
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.EmployeeId == employeeId, ct);
        var records = await db.EmploymentRecords.AsNoTracking().Where(r => r.EmployeeId == employeeId && r.IsCurrent)
            .Select(r => new { r.EmploymentRecordId, Start = r.StartDate ?? r.HireDate, r.EndDate, Status = r.EmploymentStatus == null ? null : r.EmploymentStatus.Name }).ToListAsync(ct);
        var r = records.Count == 1 ? records[0] : null;
        bool open = r is not null && !r.EndDate.HasValue;
        return ServiceResult<EmployeeAccountLifecycleDto>.Success(new(employeeId, u is not null, u?.Id, u is null ? null : u.IsActive ? "Active" : "Disabled",
            u?.AdministrationVersion, r?.EmploymentRecordId, records.Count > 1 ? "RequiresReview" : r?.Status,
            open && r!.Start <= DateOnly.FromDateTime(DateTime.UtcNow), open && u?.IsActive == true));
    }

    public static ApiFailure? ValidateDecision(bool active, bool? disable, bool securityCapability, string? expectedVersion, string currentVersion)
    {
        if (!active) return null;
        if (!disable.HasValue) return new("active_linked_account_requires_offboarding_decision", "An active linked account requires an explicit disable or retain decision.");
        if (!securityCapability) return new("forbidden", "Resolving linked account access requires Security.Manage in addition to Employee.Manage.");
        if (expectedVersion != currentVersion) return new("conflict", "Linked account changed or its current administration version was not supplied. Reload account readiness.");
        return null;
    }

    public async Task<ApiFailure?> ResolveEndAsync(Guid employeeId, Guid employmentRecordId, EndEmploymentRequest request, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Offboarding requires the employment transaction.");
        var id = await db.Users.AsNoTracking().Where(u => u.EmployeeId == employeeId).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);
        var u = id.HasValue ? await AccountLock.LockAsync(db, id.Value, employeeId, ct) : null;
        var error = ValidateDecision(u?.IsActive == true, request.DisableLinkedAccount, actor.HasCapability("Security.Manage"), request.ExpectedLinkedAccountVersion, u?.AdministrationVersion ?? "");
        if (error is not null) return error;
        if (u?.IsActive == true && request.DisableLinkedAccount == true)
        {
            error = await SystemAdminGuard.CanRemoveAsync(db, u, ct);
            if (error is not null) return error;
        }
        string decision = u is null ? "NoLinkedAccount" : !u.IsActive ? "AlreadyDisabled" : request.DisableLinkedAccount == true ? "Disabled" : "Retained";
        if (u?.IsActive == true)
        {
            u.AdministrationVersion = Guid.NewGuid().ToString();
            IdentityResult result;
            if (request.DisableLinkedAccount == true) { u.IsActive = false; result = await users.UpdateSecurityStampAsync(u); }
            else result = await users.UpdateAsync(u); // Concurrency token changes; retained sessions keep their security stamp.
            if (!result.Succeeded) return new("conflict", "Concurrent linked account change. Reload before offboarding.");
            db.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, Operation = $"OffboardingAccount{decision};EmployeeId={employeeId};EmploymentRecordId={employmentRecordId}", ResourceType = "User", ResourceId = u.Id.ToString() });
        }
        db.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, Operation = $"EmploymentEnded;AccountAccess={decision};UserId={u?.Id}", ResourceType = "EmploymentRecord", ResourceId = employmentRecordId.ToString() });
        return null;
    }
}
