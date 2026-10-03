using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed partial class LeaveFoundationService
{
    private static LeavePolicyDto PolicyDto(LeavePolicy x) => new(x.Id, x.LeaveTypeId, x.Version, x.EffectiveFrom, x.EffectiveTo, x.Status,
        x.PublishedAt.HasValue ? Utc(x.PublishedAt.Value) : null, x.BalanceTracked, x.ForeseeableNoticeHours, x.AllowsSuddenRequest, x.SupportingDocumentPolicy, x.DocumentTypeId,
        x.CertificateAfterConsecutiveDays, x.CertificateOnMondayWorkingDate, x.CertificateOnFridayWorkingDate, x.SandwichParticipation);
    public async Task<IReadOnlyList<LeavePolicyDto>> PoliciesAsync(Guid? leaveTypeId, CancellationToken ct)
        => (await db.Set<LeavePolicy>().AsNoTracking().Where(x => !leaveTypeId.HasValue || x.LeaveTypeId == leaveTypeId)
            .OrderBy(x => x.LeaveTypeId).ThenBy(x => x.EffectiveFrom).ThenBy(x => x.Version).ToListAsync(ct)).Select(PolicyDto).ToArray();
    private async Task<bool> LockLeaveType(Guid id, CancellationToken ct)
    {
        // LeaveType uses TPC; match EmploymentStatusService's scalar parent lock rather than FromSql on the derived DbSet.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [LeaveTypes] WITH (UPDLOCK) WHERE [Id] = {id}", ct);
        return await db.LeaveTypes.AnyAsync(x => x.Id == id, ct);
    }
    private async Task<string?> PolicyValidation(LeavePolicyRequest r, CancellationToken ct)
    {
        if (!r.LeaveTypeId.HasValue || !await db.LeaveTypes.AnyAsync(x => x.Id == r.LeaveTypeId && x.IsActive, ct)) return "LeaveTypeId must reference an active leave type.";
        if (string.IsNullOrWhiteSpace(r.Version) || r.Version.Length > 50 || !r.EffectiveFrom.HasValue || r.EffectiveTo < r.EffectiveFrom) return "Version and valid inclusive effective dates are required.";
        if (r.ForeseeableNoticeHours < 0 || r.CertificateAfterConsecutiveDays < 0) return "Notice hours and certificate threshold must be nonnegative.";
        if (r.SupportingDocumentPolicy is not ("None" or "AlwaysRequired" or "Conditional")) return "SupportingDocumentPolicy must be None, AlwaysRequired, or Conditional.";
        var triggers = r.CertificateAfterConsecutiveDays.HasValue || r.CertificateOnMondayWorkingDate || r.CertificateOnFridayWorkingDate;
        if (r.SupportingDocumentPolicy == "None" && (r.DocumentTypeId.HasValue || triggers)) return "None cannot configure a document type or certificate triggers.";
        if (r.SupportingDocumentPolicy != "None" && (!r.DocumentTypeId.HasValue || !await db.DocumentTypes.AnyAsync(x => x.Id == r.DocumentTypeId && x.IsActive, ct))) return "Document policy requires an active DocumentTypeId.";
        if (r.SupportingDocumentPolicy == "Conditional" && !triggers) return "Conditional document policy requires at least one typed certificate trigger.";
        if (r.SupportingDocumentPolicy == "AlwaysRequired" && triggers) return "Certificate triggers belong to Conditional document policy.";
        return null;
    }
    public async Task<ServiceResult<LeavePolicyDto>> SavePolicyAsync(Guid? id, LeavePolicyRequest r, CancellationToken ct)
    {
        if (!r.LeaveTypeId.HasValue) return Invalid<LeavePolicyDto>("LeaveTypeId is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await LockLeaveType(r.LeaveTypeId.Value, ct)) return Invalid<LeavePolicyDto>("Leave type was not found.");
        var x = id.HasValue ? await db.Set<LeavePolicy>().SingleOrDefaultAsync(x => x.Id == id, ct) : new LeavePolicy { LeaveTypeId = r.LeaveTypeId.Value };
        if (x is null) return Missing<LeavePolicyDto>("Leave policy was not found.");
        if (x.LeaveTypeId != r.LeaveTypeId) return Invalid<LeavePolicyDto>("A policy cannot change its leave type.");
        if (x.Status != "Draft") return Conflict<LeavePolicyDto>("Published leave policies are immutable.");
        var error = await PolicyValidation(r, ct); if (error is not null) return Invalid<LeavePolicyDto>(error);
        x.Version = r.Version.Trim(); x.EffectiveFrom = r.EffectiveFrom!.Value; x.EffectiveTo = r.EffectiveTo;
        x.BalanceTracked = r.BalanceTracked; x.ForeseeableNoticeHours = r.ForeseeableNoticeHours; x.AllowsSuddenRequest = r.AllowsSuddenRequest;
        x.SupportingDocumentPolicy = r.SupportingDocumentPolicy; x.DocumentTypeId = r.DocumentTypeId;
        x.CertificateAfterConsecutiveDays = r.CertificateAfterConsecutiveDays; x.CertificateOnMondayWorkingDate = r.CertificateOnMondayWorkingDate;
        x.CertificateOnFridayWorkingDate = r.CertificateOnFridayWorkingDate; x.SandwichParticipation = r.SandwichParticipation;
        if (!id.HasValue) db.Add(x);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (Unique(e)) { return Conflict<LeavePolicyDto>("Leave type/version already exists."); }
        return ServiceResult<LeavePolicyDto>.Success(PolicyDto(x));
    }
    public async Task<ServiceResult<LeavePolicyDto>> PublishPolicyAsync(Guid id, CancellationToken ct)
    {
        var parent = await db.Set<LeavePolicy>().AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.LeaveTypeId).SingleOrDefaultAsync(ct);
        if (!parent.HasValue) return Missing<LeavePolicyDto>("Leave policy was not found.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockLeaveType(parent.Value, ct);
        var x = await db.Set<LeavePolicy>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (x is null) return Missing<LeavePolicyDto>("Leave policy was not found.");
        if (x.Status != "Draft") return Conflict<LeavePolicyDto>("Published leave policies are immutable.");
        if (!await db.LeaveTypes.AnyAsync(y => y.Id == parent && y.IsActive, ct) || (x.DocumentTypeId.HasValue && !await db.DocumentTypes.AnyAsync(y => y.Id == x.DocumentTypeId && y.IsActive, ct)))
            return Invalid<LeavePolicyDto>("Publication requires active referenced master data.");
        if (await db.Set<LeavePolicy>().AnyAsync(y => y.LeaveTypeId == x.LeaveTypeId && y.Status == "Published" && (!y.EffectiveTo.HasValue || y.EffectiveTo >= x.EffectiveFrom) && (!x.EffectiveTo.HasValue || y.EffectiveFrom <= x.EffectiveTo), ct))
            return Conflict<LeavePolicyDto>("Published policy coverage overlaps. An open-ended predecessor is not automatically shortened.");
        x.Status = "Published"; x.PublishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<LeavePolicyDto>.Success(PolicyDto(x));
    }
    public async Task<ServiceResult<LeavePolicyDto>> ResolvePolicyAsync(Guid type, DateOnly date, CancellationToken ct)
    {
        var policies = await db.Set<LeavePolicy>().AsNoTracking().Where(x => x.LeaveTypeId == type && x.Status == "Published" && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).Take(2).ToListAsync(ct);
        return policies.Count == 1 ? ServiceResult<LeavePolicyDto>.Success(PolicyDto(policies[0])) : Conflict<LeavePolicyDto>(policies.Count == 0 ? "Leave policy not configured for the requested date." : "Published leave policy resolution is ambiguous.");
    }

    private async Task<EntitlementDto> EntitlementDtoAsync(EmployeeLeaveEntitlement x, CancellationToken ct)
    {
        var adjustments = await db.Set<EmployeeLeaveEntitlementAdjustment>().AsNoTracking().Where(y => y.EmployeeLeaveEntitlementId == x.Id).SumAsync(y => (long)y.AdjustmentMinutes, ct);
        return new(x.Id, x.EmployeeId, x.LeaveTypeId, x.LeaveYear, x.EntitledMinutes, adjustments, x.EntitledMinutes + adjustments);
    }
    public async Task<ServiceResult<IReadOnlyList<EntitlementDto>>> EntitlementsAsync(Guid employee, CancellationToken ct)
    {
        if (!await Exists(employee, ct)) return Missing<IReadOnlyList<EntitlementDto>>("Employee was not found.");
        // One projected SQL statement keeps each entitlement and adjustment aggregate consistent.
        var rows = await db.Set<EmployeeLeaveEntitlement>().AsNoTracking().Where(x => x.EmployeeId == employee).OrderBy(x => x.LeaveYear).ThenBy(x => x.LeaveTypeId)
            .Select(x => new { x.Id, x.EmployeeId, x.LeaveTypeId, x.LeaveYear, x.EntitledMinutes,
                Adjustment = db.Set<EmployeeLeaveEntitlementAdjustment>().Where(y => y.EmployeeLeaveEntitlementId == x.Id).Sum(y => (long)y.AdjustmentMinutes) }).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EntitlementDto>>.Success(rows.Select(x => new EntitlementDto(x.Id, x.EmployeeId, x.LeaveTypeId, x.LeaveYear, x.EntitledMinutes, x.Adjustment, x.EntitledMinutes + x.Adjustment)).ToArray());
    }
    public async Task<ServiceResult<EntitlementDto>> AddEntitlementAsync(Guid employee, EntitlementRequest r, CancellationToken ct)
    {
        if (!r.LeaveTypeId.HasValue || r.LeaveYear is < 1 or > 9999 || r.EntitledMinutes < 0) return Invalid<EntitlementDto>("A leave type, calendar year 1–9999, and nonnegative integer minutes are required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employee, ct) is null) return Missing<EntitlementDto>("Employee was not found.");
        if (!await db.LeaveTypes.AnyAsync(x => x.Id == r.LeaveTypeId && x.IsActive, ct)) return Invalid<EntitlementDto>("LeaveTypeId must reference an active leave type.");
        if (await db.Set<EmployeeLeaveEntitlement>().AnyAsync(x => x.EmployeeId == employee && x.LeaveTypeId == r.LeaveTypeId && x.LeaveYear == r.LeaveYear, ct)) return Conflict<EntitlementDto>("Employee/type/year entitlement already exists.");
        var x = new EmployeeLeaveEntitlement { EmployeeId = employee, LeaveTypeId = r.LeaveTypeId.Value, LeaveYear = r.LeaveYear, EntitledMinutes = r.EntitledMinutes };
        db.Add(x); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<EntitlementDto>.Success(new(x.Id, employee, x.LeaveTypeId, x.LeaveYear, x.EntitledMinutes, 0, x.EntitledMinutes));
    }
    public async Task<ServiceResult<IReadOnlyList<EntitlementAdjustmentDto>>> AdjustmentsAsync(Guid employee, Guid id, CancellationToken ct)
    {
        if (!await db.Set<EmployeeLeaveEntitlement>().AnyAsync(x => x.Id == id && x.EmployeeId == employee, ct)) return Missing<IReadOnlyList<EntitlementAdjustmentDto>>("Entitlement was not found for this employee.");
        var rows = await db.Set<EmployeeLeaveEntitlementAdjustment>().AsNoTracking().Where(x => x.EmployeeLeaveEntitlementId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EntitlementAdjustmentDto>>.Success(rows.Select(x => new EntitlementAdjustmentDto(x.Id, x.AdjustmentMinutes, x.Reason, Utc(x.CreatedAt))).ToArray());
    }
    public async Task<ServiceResult<EntitlementDto>> AdjustAsync(Guid employee, Guid id, EntitlementAdjustmentRequest r, CancellationToken ct)
    {
        if (r.AdjustmentMinutes == 0 || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000) return Invalid<EntitlementDto>("A nonzero signed adjustment and reason up to 1000 characters are required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employee, ct) is null) return Missing<EntitlementDto>("Employee was not found.");
        var x = await db.Set<EmployeeLeaveEntitlement>().SingleOrDefaultAsync(x => x.EmployeeId == employee && x.Id == id, ct);
        if (x is null) return Missing<EntitlementDto>("Entitlement was not found for this employee.");
        var before = await EntitlementDtoAsync(x, ct);
        if (before.AdjustedEntitledMinutes + r.AdjustmentMinutes < 0 || before.AdjustedEntitledMinutes + r.AdjustmentMinutes > int.MaxValue)
            return Invalid<EntitlementDto>("Adjusted entitlement must remain between zero and Int32.MaxValue minutes.");
        var committed = await (from a in db.Set<EmployeeLeaveAllocation>() join l in db.EmployeeLeaves on a.EmployeeLeaveId equals l.LeaveId
            where l.EmployeeId == employee && l.LeaveTypeId == x.LeaveTypeId && l.BalanceTracked == true && a.LeaveYear == x.LeaveYear
                && (l.Status == "Pending" || l.Status == "Approved") select (long)a.ChargeableMinutes).SumAsync(ct);
        if (before.AdjustedEntitledMinutes + r.AdjustmentMinutes < committed)
            return Conflict<EntitlementDto>("Adjusted entitlement cannot fall below Approved usage plus Pending reservations.");
        db.Add(new EmployeeLeaveEntitlementAdjustment { EmployeeLeaveEntitlementId = id, AdjustmentMinutes = r.AdjustmentMinutes, Reason = r.Reason.Trim(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        var result = await EntitlementDtoAsync(x, ct); await tx.CommitAsync(ct);
        return ServiceResult<EntitlementDto>.Success(result);
    }
}
