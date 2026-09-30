using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeLeaveService(SIAMISDbContext db) : IEmployeeLeaveService
{
    private static readonly IReadOnlyDictionary<string, string> AllowedStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Pending"] = "Pending",
        ["Approved"] = "Approved",
        ["Rejected"] = "Rejected",
        ["Cancelled"] = "Cancelled"
    };

    public async Task<ServiceResult<IReadOnlyList<EmployeeLeaveDto>>> GetLeavesAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<IReadOnlyList<EmployeeLeaveDto>>("Employee was not found.");
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            return Invalid<IReadOnlyList<EmployeeLeaveDto>>("fromDate cannot be after toDate.");

        var query = LeaveQuery().Where(x => x.EmployeeId == employeeId);
        if (fromDate.HasValue) query = query.Where(x => x.EndDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.StartDate <= toDate.Value);
        var leaves = await query.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.LeaveId).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeLeaveDto>>.Success(leaves);
    }

    public async Task<ServiceResult<EmployeeLeaveDto>> GetLeaveAsync(Guid employeeId, Guid leaveId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeLeaveDto>("Employee was not found.");
        var leave = await LeaveQuery().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveId == leaveId, ct);
        return leave is null
            ? NotFound<EmployeeLeaveDto>("Leave record was not found for this employee.")
            : ServiceResult<EmployeeLeaveDto>.Success(leave);
    }

    public async Task<ServiceResult<EmployeeLeaveDto>> CreateLeaveAsync(Guid employeeId, EmployeeLeaveRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeLeaveDto>("Employee was not found.");
        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeeLeaveDto>(validation);
        var status = GetStatus(request.Status, useDefault: true, out var statusError);
        if (statusError is not null) return Invalid<EmployeeLeaveDto>(statusError);

        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;
        if (BlocksOtherLeaves(status!) && await HasOverlap(employeeId, start, end, null, ct))
            return Conflict<EmployeeLeaveDto>("The requested dates overlap another active or pending leave record.");

        var leave = new EmployeeLeave
        {
            EmployeeId = employeeId,
            LeaveTypeId = request.LeaveTypeId!.Value,
            StartDate = start,
            EndDate = end,
            Days = InclusiveDays(start, end),
            Reason = Clean(request.Reason),
            Status = status!,
            Remarks = Clean(request.Remarks)
        };
        db.EmployeeLeaves.Add(leave);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetLeaveAsync(employeeId, leave.LeaveId, ct);
    }

    public async Task<ServiceResult<EmployeeLeaveDto>> UpdateLeaveAsync(Guid employeeId, Guid leaveId, EmployeeLeaveRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeLeaveDto>("Employee was not found.");
        var leave = await db.EmployeeLeaves.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveId == leaveId, ct);
        if (leave is null) return NotFound<EmployeeLeaveDto>("Leave record was not found for this employee.");
        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeeLeaveDto>(validation);
        string? statusError = null;
        var status = request.Status is null ? leave.Status : GetStatus(request.Status, useDefault: false, out statusError);
        if (request.Status is not null && statusError is not null) return Invalid<EmployeeLeaveDto>(statusError);

        var start = request.StartDate!.Value;
        var end = request.EndDate!.Value;
        if (BlocksOtherLeaves(status!) && await HasOverlap(employeeId, start, end, leaveId, ct))
            return Conflict<EmployeeLeaveDto>("The requested dates overlap another active or pending leave record.");

        leave.LeaveTypeId = request.LeaveTypeId!.Value;
        leave.StartDate = start;
        leave.EndDate = end;
        leave.Days = InclusiveDays(start, end);
        leave.Reason = Clean(request.Reason);
        leave.Status = status!;
        leave.Remarks = Clean(request.Remarks);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetLeaveAsync(employeeId, leaveId, ct);
    }

    public async Task<ServiceResult<bool>> DeleteLeaveAsync(Guid employeeId, Guid leaveId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var leave = await db.EmployeeLeaves.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.LeaveId == leaveId, ct);
        if (leave is null) return NotFound<bool>("Leave record was not found for this employee.");
        db.EmployeeLeaves.Remove(leave);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<string?> ValidateRequest(EmployeeLeaveRequest request, CancellationToken ct)
    {
        if (!request.LeaveTypeId.HasValue || !await db.LeaveTypes.AsNoTracking().AnyAsync(x => x.Id == request.LeaveTypeId && x.IsActive, ct))
            return "LeaveTypeId must reference an active leave type.";
        if (!request.StartDate.HasValue) return "StartDate is required.";
        if (!request.EndDate.HasValue) return "EndDate is required.";
        if (request.EndDate.Value < request.StartDate.Value) return "EndDate cannot be before StartDate.";
        if (request.Reason?.Length > 1000) return "Reason cannot exceed 1000 characters.";
        if (request.Remarks?.Length > 2000) return "Remarks cannot exceed 2000 characters.";
        return null;
    }

    private Task<bool> HasOverlap(Guid employeeId, DateOnly start, DateOnly end, Guid? excludedLeaveId, CancellationToken ct)
        => db.EmployeeLeaves.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId
            && x.Status != "Rejected" && x.Status != "Cancelled"
            && x.StartDate <= end && x.EndDate >= start
            && (!excludedLeaveId.HasValue || x.LeaveId != excludedLeaveId.Value), ct);

    private IQueryable<EmployeeLeaveDto> LeaveQuery() => db.EmployeeLeaves.AsNoTracking().Select(x => new EmployeeLeaveDto
    {
        LeaveId = x.LeaveId,
        EmployeeId = x.EmployeeId,
        LeaveTypeId = x.LeaveTypeId,
        LeaveTypeCode = x.LeaveType.Code,
        LeaveTypeName = x.LeaveType.Name,
        StartDate = x.StartDate,
        EndDate = x.EndDate,
        Days = x.Days,
        Reason = x.Reason,
        Status = x.Status,
        Remarks = x.Remarks
    });

    private static int InclusiveDays(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;
    private static bool BlocksOtherLeaves(string status)
        => !string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    private static string? GetStatus(string? supplied, bool useDefault, out string? error)
    {
        error = null;
        if (supplied is null && useDefault) return "Pending";
        if (string.IsNullOrWhiteSpace(supplied) || !AllowedStatuses.TryGetValue(supplied.Trim(), out var canonical))
        {
            error = "Status must be Pending, Approved, Rejected, or Cancelled.";
            return null;
        }
        return canonical;
    }

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
}
