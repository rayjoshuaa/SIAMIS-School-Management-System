using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class AttendanceDayService(SIAMISDbContext db) : IAttendanceDayService
{
    public async Task<ServiceResult<AttendanceDayDto>> GetAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        if (date == DateOnly.MinValue) return ServiceResult<AttendanceDayDto>.Fail("validation", "Business date must permit conversion of Bangkok midnight to UTC (0001-01-02 or later).");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var employee = await EmploymentIntegrity.LockAsync(db, employeeId, ct);
            if (employee is null) return ServiceResult<AttendanceDayDto>.Fail("not_found", "Employee was not found.");
            var result = await CalculateLockedAsync(db, employee, date, ct);
            await tx.CommitAsync(ct);
            return ServiceResult<AttendanceDayDto>.Success(result);
        }
        catch (SqlException e) when (e.Number == 1205)
        {
            return ServiceResult<AttendanceDayDto>.Fail("conflict", "Concurrent attendance context changed. Retry the daily read.");
        }
    }
    // Caller holds the Employee-first transaction. D9C and D9D share the exact source queries.
    internal static async Task<AttendanceDayDto> CalculateLockedAsync(SIAMISDbContext db, SIAMIS.Domain.Entities.Employees.Employee employee, DateOnly date, CancellationToken ct)
    {
        var employeeId = employee.EmployeeId;
        var work = await AttendanceFoundationService.ResolveExpectedWorkAsync(db, employee, date, ct);
        var events = await db.AttendanceEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.BusinessDate == date)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.AttendanceEventId)
            .Select(e => new AttendanceEventDto(e.AttendanceEventId, e.EmployeeId, DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc),
                e.BusinessDate, e.BusinessTimeZone, e.Direction, e.Source, e.SourceKey, e.ExternalEventId, e.ManualRequestKey,
                e.OriginalSourceTimestamp, DateTime.SpecifyKind(e.ReceivedAtUtc, DateTimeKind.Utc), e.Reason, e.ActorId, e.EmployeeWasInactive, e.EmploymentReadiness)).ToListAsync(ct);
        var headers = await db.EmployeeLeaves.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.Status == "Approved" && x.StartDate <= date && x.EndDate >= date).ToListAsync(ct);
        var ids = headers.Select(x => x.LeaveId).ToArray();
        var allocations = await db.Set<EmployeeLeaveAllocation>().AsNoTracking().Where(x => ids.Contains(x.EmployeeLeaveId)).ToListAsync(ct);
        var leaves = new List<AttendanceLeaveEvidence>(); var findings = new List<AttendanceDayFinding>();
        foreach (var header in headers.OrderBy(x => x.LeaveId))
        {
            var read = LeaveSnapshotIntegrity.Read(header, allocations.Where(x => x.EmployeeLeaveId == header.LeaveId).ToArray());
            if (!read.IsSuccess) findings.Add(new("LeaveSnapshotInvalid", read.Failure!.Message, [header.LeaveId]));
            else leaves.Add(new(header.LeaveId, header.Status, read.Value!.Version, read.Value.IsPaid!.Value, read.Value.Dates.Single(x => x.Date == date)));
        }
        return AttendanceDayCalculator.Calculate(work, events, leaves, findings, DateTime.UtcNow);
    }
}
