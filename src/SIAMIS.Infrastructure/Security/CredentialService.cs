using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Security;

public sealed class CredentialService(SIAMISDbContext db, UserManager<ApplicationUser> users, ICredentialDelivery delivery, ICurrentActor actor) : ICredentialService
{
    public const string ActivationPurpose = "SIAMIS.InitialActivation.v1";
    public static bool RecoveryEligible(ApplicationUser u) => u.IsActive && u.PasswordHash is not null && u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email);
    public static bool ActivationEligible(ApplicationUser u) => u.IsActive && u.PasswordHash is null && !u.EmailConfirmed && !string.IsNullOrWhiteSpace(u.Email);
    private async Task<bool> DeliverAsync(ApplicationUser u, bool activation, CancellationToken ct)
    {
        if (!delivery.IsConfigured || !(activation ? ActivationEligible(u) : RecoveryEligible(u))) return false;
        string token = activation ? await users.GenerateUserTokenAsync(u, users.Options.Tokens.PasswordResetTokenProvider, ActivationPurpose)
            : await users.GeneratePasswordResetTokenAsync(u);
        try { return await delivery.DeliverAsync(new(u.Id, u.Email!, activation ? "Activation" : "PasswordReset", token), ct); }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) { return false; }
    }
    public async Task<bool> DeliverInitialActivationAsync(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Provisioning delivery requires its transaction.");
        var u = await users.FindByIdAsync(id.ToString());
        return u is not null && await DeliverAsync(u, true, ct);
    }
    public async Task ForgotAsync(ForgotPasswordRequest r, CancellationToken ct)
    {
        if (!delivery.IsConfigured || string.IsNullOrWhiteSpace(r.Email)) return;
        var email = users.NormalizeEmail(r.Email.Trim());
        var id = await db.Users.AsNoTracking().Where(u => u.NormalizedEmail == email).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);
        if (!id.HasValue) return;
        // A reset request does not rotate the stamp or revoke sessions. Successful consumption does.
        try
        {
            var employee = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.EmployeeId).SingleOrDefaultAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var u = await AccountLock.LockAsync(db, id.Value, employee, ct);
            if (u is not null && u.NormalizedEmail == email && RecoveryEligible(u)) await DeliverAsync(u, false, ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception e) when (e is SqlException { Number: 1205 } || e is DbUpdateConcurrencyException) { /* Same generic public outcome. */ }
    }
    public async Task<ServiceResult<CredentialIssuedDto>> IssueAsync(Guid id, IssueCredentialRequest r, CancellationToken ct)
    {
        if (!actor.HasCapability("Security.Manage")) return ServiceResult<CredentialIssuedDto>.Fail("forbidden", "Security.Manage is required.");
        if (!delivery.IsConfigured) return ServiceResult<CredentialIssuedDto>.Fail("delivery_unavailable", "Credential delivery is not configured.");
        try
        {
            var employee = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.EmployeeId).SingleOrDefaultAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var u = await AccountLock.LockAsync(db, id, employee, ct);
            if (u is null) return ServiceResult<CredentialIssuedDto>.Fail("not_found", "User was not found.");
            if (u.AdministrationVersion != r.Version) return ServiceResult<CredentialIssuedDto>.Fail("conflict", "User changed. Reload before issuing credentials.");
            bool activation = ActivationEligible(u);
            if (!activation && !RecoveryEligible(u)) return ServiceResult<CredentialIssuedDto>.Fail("conflict", "Account is not eligible for activation or verified-email recovery.");
            // Activation reissue stales earlier invitations. Reset issuance leaves existing sessions intact.
            u.AdministrationVersion = Guid.NewGuid().ToString();
            var result = activation ? await users.UpdateSecurityStampAsync(u) : await users.UpdateAsync(u);
            if (!result.Succeeded) return ServiceResult<CredentialIssuedDto>.Fail("conflict", "Concurrent credential state change.");
            if (!await DeliverAsync(u, activation, ct)) return ServiceResult<CredentialIssuedDto>.Fail("delivery_unavailable", "Credential delivery is unavailable.");
            db.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, Operation = activation ? "ActivationReissued" : "PasswordRecoveryInitiated", ResourceType = "User", ResourceId = id.ToString() });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ServiceResult<CredentialIssuedDto>.Success(new(activation ? "Activation" : "PasswordReset"));
        }
        catch (Exception e) when (e is SqlException { Number: 1205 } || e is DbUpdateConcurrencyException || e is DbUpdateException { InnerException: SqlException { Number: 1205 } })
        { return ServiceResult<CredentialIssuedDto>.Fail("conflict", "Concurrent account state change."); }
    }
    public async Task<bool> CompleteAsync(CompleteCredentialRequest r, bool activation, CancellationToken ct)
    {
        if (!r.UserId.HasValue || r.UserId == Guid.Empty) return false;
        try
        {
            var employee = await db.Users.AsNoTracking().Where(u => u.Id == r.UserId).Select(u => u.EmployeeId).SingleOrDefaultAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var u = await AccountLock.LockAsync(db, r.UserId.Value, employee, ct);
            if (u is null || !(activation ? ActivationEligible(u) : RecoveryEligible(u))) return false;
            IdentityResult result;
            if (activation)
            {
                if (!await users.VerifyUserTokenAsync(u, users.Options.Tokens.PasswordResetTokenProvider, ActivationPurpose, r.Token)) return false;
                result = await users.AddPasswordAsync(u, r.NewPassword);
            }
            else result = await users.ResetPasswordAsync(u, r.Token, r.NewPassword);
            if (!result.Succeeded) return false;
            if (activation) u.EmailConfirmed = true;
            u.RequiresPasswordChange = false; u.AdministrationVersion = Guid.NewGuid().ToString();
            if (!(await users.UpdateAsync(u)).Succeeded) return false;
            // Anonymous actor remains null; target ID is historical attribution, not proof supplied by the caller.
            db.Add(new SecurityAuditEvent { ActorUserId = actor.UserId, Operation = activation ? "AccountActivated" : "PasswordResetCompleted", ResourceType = "User", ResourceId = u.Id.ToString() });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
        }
        catch (Exception e) when (e is SqlException { Number: 1205 } || e is DbUpdateConcurrencyException || e is DbUpdateException { InnerException: SqlException { Number: 1205 } }) { return false; }
    }
}
