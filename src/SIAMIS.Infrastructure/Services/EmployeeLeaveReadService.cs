using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;

namespace SIAMIS.Infrastructure.Services;

public sealed partial class EmployeeLeaveService
{
    private Task<bool> Exists(Guid employee, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employee, ct);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
    private static EmployeeLeaveDto Dto(EmployeeLeave x, string? code, string name, IReadOnlyList<EmployeeLeaveAllocation> allocations)
    {
        LeaveCalculationSnapshot? snapshot = null;
        if (x.CalculationSnapshotVersion == 1 && x.CalculationSnapshotJson is not null)
        {
            var integrity = LeaveSnapshotIntegrity.Read(x, allocations);
            if (integrity.IsSuccess) snapshot = integrity.Value;
        }
        return new EmployeeLeaveDto
        {
            LeaveId = x.LeaveId, EmployeeId = x.EmployeeId, LeaveTypeId = x.LeaveTypeId, LeaveTypeCode = snapshot?.LeaveTypeCode ?? code,
            LeaveTypeName = snapshot?.LeaveTypeName ?? name, IsPaid = snapshot?.IsPaid, StartDate = x.StartDate, EndDate = x.EndDate, Days = x.Days, Reason = x.Reason,
            Status = x.Status, Remarks = x.Remarks, RequestMode = x.RequestMode, NoticeCategory = x.NoticeCategory,
            RequestedStartTime = x.RequestedStartTime, RequestedEndTime = x.RequestedEndTime, ChargeableMinutes = x.ChargeableMinutes,
            RequestedAt = Utc(x.RequestedAt), ReviewedAt = Utc(x.ReviewedAt), CancelledAt = Utc(x.CancelledAt), ReviewRemarks = x.ReviewRemarks,
            CancellationRemarks = x.CancellationRemarks, Calculation = snapshot, SupportingDocumentRequired = snapshot?.SupportingDocumentRequired,
            CertificateRequirementReasons = snapshot?.CertificateRequirementReasons ?? [],
            Allocations = allocations.OrderBy(a => a.LeaveYear).Select(a => new LeaveAllocationDto(a.LeaveYear, a.ChargeableMinutes)).ToArray()
        };
    }
    public async Task<ServiceResult<IReadOnlyList<EmployeeLeaveDto>>> GetLeavesAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        if (!await Exists(employeeId, ct)) return Fail<IReadOnlyList<EmployeeLeaveDto>>("not_found", "Employee was not found.");
        if (fromDate > toDate) return Fail<IReadOnlyList<EmployeeLeaveDto>>("validation", "fromDate cannot be after toDate.");
        var records = await db.EmployeeLeaves.AsNoTracking().Where(x => x.EmployeeId == employeeId && (!fromDate.HasValue || x.EndDate >= fromDate) && (!toDate.HasValue || x.StartDate <= toDate))
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.LeaveId).Select(x => new { Leave = x, x.LeaveType.Code, x.LeaveType.Name }).ToListAsync(ct);
        var ids = records.Select(x => x.Leave.LeaveId).ToArray();
        var allocations = await db.Set<EmployeeLeaveAllocation>().AsNoTracking().Where(x => ids.Contains(x.EmployeeLeaveId)).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeLeaveDto>>.Success(records.Select(x => Dto(x.Leave, x.Code, x.Name, allocations.Where(a => a.EmployeeLeaveId == x.Leave.LeaveId).ToArray())).ToArray());
    }
    public async Task<ServiceResult<EmployeeLeaveDto>> GetLeaveAsync(Guid employeeId, Guid leaveId, CancellationToken ct)
    {
        if (!await Exists(employeeId, ct)) return Fail<EmployeeLeaveDto>("not_found", "Employee was not found.");
        var x = await db.EmployeeLeaves.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.LeaveId == leaveId).Select(x => new { Leave = x, x.LeaveType.Code, x.LeaveType.Name }).SingleOrDefaultAsync(ct);
        return x is null ? Fail<EmployeeLeaveDto>("not_found", "Leave request was not found for this employee.")
            : ServiceResult<EmployeeLeaveDto>.Success(Dto(x.Leave, x.Code, x.Name, await Allocations(leaveId, ct)));
    }
    public async Task<ServiceResult<PagedResult<LeaveHistoryItemDto>>> HistoryAsync(LeaveHistoryQuery r, bool pendingOnly, CancellationToken ct)
    {
        if (r.Page < 1 || r.PageSize is < 1 or > 100 || (long)(r.Page - 1) * r.PageSize > int.MaxValue || r.FromDate > r.ToDate)
            return Fail<PagedResult<LeaveHistoryItemDto>>("validation", "Invalid pagination or date range.");
        if (r.Status is not null && r.Status is not ("Pending" or "Approved" or "Rejected" or "Cancelled")) return Fail<PagedResult<LeaveHistoryItemDto>>("validation", "Status must be Pending, Approved, Rejected, or Cancelled.");
        if (pendingOnly && r.Status is not null && r.Status != "Pending") return Fail<PagedResult<LeaveHistoryItemDto>>("validation", "The pending queue only supports Pending status.");
        var query = db.EmployeeLeaves.AsNoTracking().Where(x => (!r.EmployeeId.HasValue || x.EmployeeId == r.EmployeeId) && (!r.LeaveTypeId.HasValue || x.LeaveTypeId == r.LeaveTypeId)
            && (!r.FromDate.HasValue || x.EndDate >= r.FromDate) && (!r.ToDate.HasValue || x.StartDate <= r.ToDate) && (!pendingOnly || x.Status == "Pending") && (r.Status == null || x.Status == r.Status));
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.LeaveId).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(x => new { Leave = x, x.Employee.EmployeeNumber, EmployeeName = x.Employee.FirstName + " " + x.Employee.LastName, x.LeaveType.Code, x.LeaveType.Name }).ToListAsync(ct);
        var items = rows.Select(x =>
        {
            LeaveCalculationSnapshot? s = null;
            try { if (x.Leave.CalculationSnapshotVersion == 1 && x.Leave.CalculationSnapshotJson is not null) s = JsonSerializer.Deserialize<LeaveCalculationSnapshot>(x.Leave.CalculationSnapshotJson, SnapshotJson); } catch (JsonException) { }
            var l = x.Leave;
            return new LeaveHistoryItemDto(l.LeaveId, l.EmployeeId, x.EmployeeNumber, x.EmployeeName, l.LeaveTypeId, s?.LeaveTypeCode ?? x.Code, s?.LeaveTypeName ?? x.Name,
                s?.IsPaid, l.StartDate, l.EndDate, l.RequestMode, l.RequestedStartTime, l.RequestedEndTime, l.ChargeableMinutes, l.Status, Utc(l.RequestedAt), l.NoticeCategory, s?.SupportingDocumentRequired, l.Reason);
        }).ToArray();
        return ServiceResult<PagedResult<LeaveHistoryItemDto>>.Success(new(items, r.Page, r.PageSize, count));
    }
    public async Task<ServiceResult<IReadOnlyList<LeaveBalanceDto>>> BalancesAsync(Guid employeeId, int year, CancellationToken ct)
    {
        if (year is < 1 or > 9999) return Fail<IReadOnlyList<LeaveBalanceDto>>("validation", "Leave year must be between 1 and 9999.");
        // Read-only Serializable keeps policies, base entitlement, adjustments and allocations in one coherent balance view.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await Exists(employeeId, ct)) return Fail<IReadOnlyList<LeaveBalanceDto>>("not_found", "Employee was not found.");
        var first = new DateOnly(year, 1, 1); var last = new DateOnly(year, 12, 31);
        var types = await db.LeaveTypes.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
        var policies = await db.Set<LeavePolicy>().AsNoTracking().Where(x => x.Status == "Published" && x.EffectiveFrom <= last && (!x.EffectiveTo.HasValue || x.EffectiveTo >= first)).ToListAsync(ct);
        var entitlements = await db.Set<EmployeeLeaveEntitlement>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.LeaveYear == year)
            .Select(x => new { x.LeaveTypeId, x.EntitledMinutes, Adjustment = db.Set<EmployeeLeaveEntitlementAdjustment>().Where(a => a.EmployeeLeaveEntitlementId == x.Id).Sum(a => (long)a.AdjustmentMinutes) }).ToListAsync(ct);
        var amounts = await (from a in db.Set<EmployeeLeaveAllocation>().AsNoTracking() join l in db.EmployeeLeaves.AsNoTracking() on a.EmployeeLeaveId equals l.LeaveId
            where l.EmployeeId == employeeId && l.BalanceTracked == true && a.LeaveYear == year && (l.Status == "Pending" || l.Status == "Approved")
            select new { l.LeaveTypeId, l.Status, a.ChargeableMinutes }).ToListAsync(ct);
        var result = types.Select(t =>
        {
            var flags = policies.Where(p => p.LeaveTypeId == t.Id).Select(p => p.BalanceTracked).Distinct().ToArray();
            bool? tracked = flags.Length == 1 ? flags[0] : null;
            string coverage = flags.Length == 0 ? "NotConfigured" : flags.Length == 2 ? "Mixed" : tracked == true ? "Tracked" : "NotTracked";
            var e = entitlements.SingleOrDefault(x => x.LeaveTypeId == t.Id);
            var pending = amounts.Where(x => x.LeaveTypeId == t.Id && x.Status == "Pending").Sum(x => (long)x.ChargeableMinutes);
            var used = amounts.Where(x => x.LeaveTypeId == t.Id && x.Status == "Approved").Sum(x => (long)x.ChargeableMinutes);
            var expose = tracked != false && e is not null;
            return new LeaveBalanceDto(t.Id, t.Code, t.Name, tracked, coverage, expose ? e!.EntitledMinutes : null, expose ? e!.Adjustment : null,
                expose ? e!.EntitledMinutes + e.Adjustment : null, pending, used, expose ? e!.EntitledMinutes + e.Adjustment - pending - used : null);
        }).ToArray();
        await tx.CommitAsync(ct);
        return ServiceResult<IReadOnlyList<LeaveBalanceDto>>.Success(result);
    }
}
