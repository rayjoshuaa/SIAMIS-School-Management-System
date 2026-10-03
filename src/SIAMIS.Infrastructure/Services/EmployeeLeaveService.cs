using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed partial class EmployeeLeaveService(SIAMISDbContext db) : IEmployeeLeaveService, SIAMIS.Application.Leave.ILeaveEvidenceSandwichService
{
    internal static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);
    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool Concurrent(Exception e) => e is SqlException { Number: 1205 or 2601 or 2627 }
        || e.InnerException is not null && Concurrent(e.InnerException);

    public async Task<ServiceResult<EmployeeLeaveDto>> CreateLeaveAsync(Guid employeeId, EmployeeLeaveRequest request, CancellationToken ct)
    {
        var error = LeaveRequestCalculator.Shape(request);
        if (error is not null) return Fail<EmployeeLeaveDto>("validation", error);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Shared with D1, assignments and entitlement writers: employee first, sorted calendars, then leave type.
            if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail<EmployeeLeaveDto>("not_found", "Employee was not found.");
            var start = request.StartDate!.Value; var end = request.EndDate!.Value;
            var assignments = await db.Set<EmployeeWorkCalendarAssignment>().AsNoTracking().Where(x => x.EmployeeId == employeeId).ToListAsync(ct);
            var calendars = new List<WorkCalendar>();
            foreach (var id in assignments.Select(x => x.WorkCalendarId).Distinct().OrderBy(x => x))
            {
                var calendar = await db.Set<WorkCalendar>().FromSqlInterpolated($"SELECT * FROM [WorkCalendars] WITH (UPDLOCK) WHERE [Id] = {id}").AsNoTracking().SingleOrDefaultAsync(ct);
                if (calendar is not null) calendars.Add(calendar);
            }
            var typeId = request.LeaveTypeId!.Value;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [LeaveTypes] WITH (UPDLOCK) WHERE [Id] = {typeId}", ct);
            var type = await db.LeaveTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == typeId, ct);
            if (type is null) return Fail<EmployeeLeaveDto>("not_found", "Leave type was not found.");
            if (!type.IsActive) return Fail<EmployeeLeaveDto>("validation", "Leave type must be active.");
            var ids = calendars.Select(x => x.Id).ToArray();
            var overrides = await db.Set<WorkCalendarDateOverride>().AsNoTracking().Where(x => ids.Contains(x.WorkCalendarId) && x.Date >= start && x.Date <= end).ToListAsync(ct);
            var overrideIds = overrides.Select(x => x.Id).ToArray();
            var context = new LeaveCalculationContext(employeeId, type,
                await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId).Where(EmploymentIntegrity.Overlapping(start, end)).ToListAsync(ct), assignments, calendars,
                await db.Set<WorkCalendarWeeklyInterval>().AsNoTracking().Where(x => ids.Contains(x.WorkCalendarId)).ToListAsync(ct), overrides,
                await db.Set<WorkCalendarOverrideInterval>().AsNoTracking().Where(x => overrideIds.Contains(x.WorkCalendarDateOverrideId)).ToListAsync(ct),
                await db.Set<LeavePolicy>().AsNoTracking().Where(x => x.LeaveTypeId == typeId && x.Status == "Published" && x.EffectiveFrom <= end && (!x.EffectiveTo.HasValue || x.EffectiveTo >= start)).ToListAsync(ct));
            var calculation = LeaveRequestCalculator.Calculate(context, request, DateTime.UtcNow);
            if (!calculation.IsSuccess) return Fail<EmployeeLeaveDto>(calculation.Failure!.Code, calculation.Failure.Message);
            var snapshot = calculation.Value!;
            var candidates = await db.EmployeeLeaves.AsNoTracking().Where(x => x.EmployeeId == employeeId && (x.Status == "Pending" || x.Status == "Approved") && x.StartDate <= end && x.EndDate >= start).ToListAsync(ct);
            foreach (var candidate in candidates)
            {
                var existing = LeaveSnapshotIntegrity.Read(candidate, await Allocations(candidate.LeaveId, ct));
                if (!existing.IsSuccess) return Fail<EmployeeLeaveDto>("conflict", existing.Failure!.Message);
                if (LeaveRequestCalculator.Overlap(snapshot, existing.Value!)) return Fail<EmployeeLeaveDto>("conflict", "The requested chargeable intervals overlap Pending or Approved leave.");
            }
            if (snapshot.BalanceTracked)
                foreach (var allocation in snapshot.Allocations)
                {
                    var balance = await AvailableAsync(employeeId, typeId, allocation.LeaveYear, ct);
                    if (!balance.IsSuccess) return Fail<EmployeeLeaveDto>("conflict", balance.Failure!.Message);
                    if (balance.Value < allocation.ChargeableMinutes) return Fail<EmployeeLeaveDto>("conflict", "Insufficient available leave balance.");
                }
            var leave = new EmployeeLeave
            {
                EmployeeId = employeeId, LeaveTypeId = typeId, StartDate = start, EndDate = end,
                RequestMode = snapshot.RequestMode, NoticeCategory = snapshot.NoticeCategory, RequestedStartTime = snapshot.RequestedStartTime, RequestedEndTime = snapshot.RequestedEndTime,
                BalanceTracked = snapshot.BalanceTracked, ChargeableMinutes = snapshot.ChargeableMinutes, RequestedAt = snapshot.RequestedAt,
                CalculationSnapshotVersion = 1, CalculationSnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotJson),
                Days = snapshot.Dates.Count(x => x.ChargeableMinutes > 0), Reason = Clean(request.Reason), Status = "Pending"
            };
            db.Add(leave);
            foreach (var allocation in snapshot.Allocations) db.Add(new EmployeeLeaveAllocation { EmployeeLeaveId = leave.LeaveId, LeaveYear = allocation.LeaveYear, ChargeableMinutes = allocation.ChargeableMinutes });
            await db.SaveChangesAsync(ct);
            var formed = await FormSandwiches(employeeId, leave.LeaveId, ct);
            if (!formed.IsSuccess) return Fail<EmployeeLeaveDto>("conflict", formed.Failure!.Message);
            await tx.CommitAsync(ct);
            return await GetLeaveAsync(employeeId, leave.LeaveId, ct);
        }
        catch (Exception e) when (Concurrent(e)) { return Fail<EmployeeLeaveDto>("conflict", "Concurrent leave configuration or reservation changed. Retry the request."); }
    }

    public Task<ServiceResult<EmployeeLeaveDto>> ApproveAsync(Guid employeeId, Guid leaveId, LeaveReviewRequest r, CancellationToken ct) => Transition(employeeId, leaveId, "Approved", r.ReviewRemarks, ct);
    public Task<ServiceResult<EmployeeLeaveDto>> RejectAsync(Guid employeeId, Guid leaveId, LeaveReviewRequest r, CancellationToken ct) => Transition(employeeId, leaveId, "Rejected", r.ReviewRemarks, ct);
    public Task<ServiceResult<EmployeeLeaveDto>> CancelAsync(Guid employeeId, Guid leaveId, LeaveCancellationRequest r, CancellationToken ct)
        => !r.ExpectedStatus.HasValue || !Enum.IsDefined(r.ExpectedStatus.Value)
            ? Task.FromResult(Fail<EmployeeLeaveDto>("validation", "ExpectedStatus must be explicitly Pending or Approved."))
            : Transition(employeeId, leaveId, "Cancelled", r.CancellationRemarks, ct, r.ExpectedStatus.Value.ToString());

    private async Task<ServiceResult<EmployeeLeaveDto>> Transition(Guid employeeId, Guid leaveId, string target, string? remarks, CancellationToken ct, string? expectedStatus = null)
    {
        if (remarks?.Length > 2000) return Fail<EmployeeLeaveDto>("validation", "Remarks cannot exceed 2000 characters.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Fail<EmployeeLeaveDto>("not_found", "Employee was not found.");
            var leave = await db.EmployeeLeaves.FromSqlInterpolated($"SELECT * FROM [EmployeeLeave] WITH (UPDLOCK) WHERE [LeaveId] = {leaveId} AND [EmployeeId] = {employeeId}").SingleOrDefaultAsync(ct);
            if (leave is null) return Fail<EmployeeLeaveDto>("not_found", "Leave request was not found for this employee.");
            // Caller-observed source state is compared only after authoritative reload, under the mutation locks.
            if (target == "Cancelled" && leave.Status != expectedStatus) return Fail<EmployeeLeaveDto>("conflict", "Leave status no longer matches ExpectedStatus.");
            if (leave.Status != "Pending" && !(leave.Status == "Approved" && target == "Cancelled")) return Fail<EmployeeLeaveDto>("conflict", "This lifecycle transition is not permitted.");
            if (leave.Status == "Approved" && string.IsNullOrWhiteSpace(remarks)) return Fail<EmployeeLeaveDto>("validation", "Approved cancellation requires CancellationRemarks.");
            var integrity = LeaveSnapshotIntegrity.Read(leave, await Allocations(leaveId, ct));
            if (!integrity.IsSuccess) return Fail<EmployeeLeaveDto>("conflict", integrity.Failure!.Message);
            if (target == "Approved" && leave.BalanceTracked == true)
                foreach (var allocation in integrity.Value!.Allocations)
                {
                    var balance = await AvailableAsync(employeeId, leave.LeaveTypeId, allocation.LeaveYear, ct);
                    if (!balance.IsSuccess) return Fail<EmployeeLeaveDto>("conflict", balance.Failure!.Message);
                    if (balance.Value < 0) return Fail<EmployeeLeaveDto>("conflict", "The stored reservation is inconsistent with the entitlement.");
                }
            if (target == "Approved")
            {
                var evidence = await FreezeApprovalEvidence(employeeId, leaveId, ct);
                if (!evidence.IsSuccess) return Fail<EmployeeLeaveDto>("conflict", evidence.Failure!.Message);
            }
            var reconciled = await ReconcileSandwiches(employeeId, leaveId, target, ct);
            if (!reconciled.IsSuccess) return Fail<EmployeeLeaveDto>("conflict", reconciled.Failure!.Message);
            leave.Status = target;
            if (target == "Cancelled") { leave.CancelledAt = DateTime.UtcNow; leave.CancellationRemarks = Clean(remarks); }
            else { leave.ReviewedAt = DateTime.UtcNow; leave.ReviewRemarks = Clean(remarks); }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await GetLeaveAsync(employeeId, leaveId, ct);
        }
        catch (Exception e) when (Concurrent(e)) { return Fail<EmployeeLeaveDto>("conflict", "Concurrent leave transition changed. Retry the request."); }
    }
    private Task<List<EmployeeLeaveAllocation>> Allocations(Guid id, CancellationToken ct) => db.Set<EmployeeLeaveAllocation>().AsNoTracking().Where(x => x.EmployeeLeaveId == id).OrderBy(x => x.LeaveYear).ToListAsync(ct);
    private async Task<ServiceResult<long>> AvailableAsync(Guid employee, Guid type, int year, CancellationToken ct)
    {
        var entitlement = await db.Set<EmployeeLeaveEntitlement>().SingleOrDefaultAsync(x => x.EmployeeId == employee && x.LeaveTypeId == type && x.LeaveYear == year, ct);
        if (entitlement is null) return Fail<long>("conflict", "Leave entitlement not configured.");
        var adjustment = await db.Set<EmployeeLeaveEntitlementAdjustment>().Where(x => x.EmployeeLeaveEntitlementId == entitlement.Id).SumAsync(x => (long)x.AdjustmentMinutes, ct);
        var reserved = await (from a in db.Set<EmployeeLeaveAllocation>() join l in db.EmployeeLeaves on a.EmployeeLeaveId equals l.LeaveId
            where l.EmployeeId == employee && l.LeaveTypeId == type && l.BalanceTracked == true && a.LeaveYear == year && (l.Status == "Pending" || l.Status == "Approved") select (long)a.ChargeableMinutes).SumAsync(ct);
        return ServiceResult<long>.Success(entitlement.EntitledMinutes + adjustment - reserved - await SandwichCommitted(employee, type, year, ct));
    }
}
