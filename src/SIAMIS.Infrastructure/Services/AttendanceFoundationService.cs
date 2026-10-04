using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class AttendanceFoundationService(SIAMISDbContext db) : IAttendanceFoundationService
{
    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
    private static AttendanceEventDto Dto(AttendanceEvent e) => new(e.AttendanceEventId, e.EmployeeId,
        DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc), e.BusinessDate, e.BusinessTimeZone, e.Direction, e.Source,
        e.SourceKey, e.ExternalEventId, e.ManualRequestKey, e.OriginalSourceTimestamp,
        DateTime.SpecifyKind(e.ReceivedAtUtc, DateTimeKind.Utc), e.Reason, e.ActorId, e.EmployeeWasInactive, e.EmploymentReadiness);
    private static ServiceResult<ManualAttendanceEventResult> Replay(AttendanceEvent e, Guid employeeId, ManualAttendanceEventRequest r, DateTime utc)
        => e.EmployeeId == employeeId && e.OccurredAtUtc == utc && e.Direction == r.Direction.ToString()
            && e.Source == "ManualAuthorized" && e.Reason == r.Reason!.Trim()
            ? ServiceResult<ManualAttendanceEventResult>.Success(new(Dto(e), true))
            : Fail<ManualAttendanceEventResult>("conflict", "ManualRequestKey already identifies different attendance evidence.");
    private static bool Concurrent(Exception e) => e is SqlException { Number: 1205 or 2601 or 2627 }
        || e.InnerException is not null && Concurrent(e.InnerException);

    public async Task<ServiceResult<ManualAttendanceEventResult>> CreateManualAsync(Guid employeeId, ManualAttendanceEventRequest r, CancellationToken ct)
    {
        if (!AttendanceFoundationResolver.TryInstant(r.OccurredAt, out var utc) || r.Direction is null || !Enum.IsDefined(r.Direction.Value)
            || r.ManualRequestKey is null || r.ManualRequestKey == Guid.Empty || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 2000)
            return Fail<ManualAttendanceEventResult>("validation", "An explicit-offset ISO timestamp, In/Out/Unknown direction, nonempty ManualRequestKey and reason up to 2000 characters are required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var employee = await EmploymentIntegrity.LockAsync(db, employeeId, ct);
            if (employee is null) return Fail<ManualAttendanceEventResult>("not_found", "Employee was not found.");
            var existing = await db.AttendanceEvents.FromSqlInterpolated($"SELECT * FROM [AttendanceEvents] WITH (UPDLOCK) WHERE [ManualRequestKey] = {r.ManualRequestKey.Value}").AsNoTracking().SingleOrDefaultAsync(ct);
            if (existing is not null) return Replay(existing, employeeId, r, utc);
            var date = AttendanceFoundationResolver.BusinessDate(utc);
            var employment = await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId).Where(EmploymentIntegrity.EffectiveOn(date)).Take(2).ToListAsync(ct);
            var e = new AttendanceEvent { EmployeeId = employeeId, OccurredAtUtc = utc, BusinessDate = date,
                Direction = r.Direction.Value.ToString(), ManualRequestKey = r.ManualRequestKey, OriginalSourceTimestamp = r.OccurredAt,
                ReceivedAtUtc = DateTime.UtcNow, Reason = r.Reason.Trim(), EmployeeWasInactive = !employee.IsActive,
                EmploymentReadiness = AttendanceFoundationResolver.EmploymentReadiness(employment) };
            db.AttendanceEvents.Add(e);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ServiceResult<ManualAttendanceEventResult>.Success(new(Dto(e), false));
        }
        catch (Exception e) when (Concurrent(e))
        {
            db.ChangeTracker.Clear();
            var existing = await db.AttendanceEvents.AsNoTracking().SingleOrDefaultAsync(x => x.ManualRequestKey == r.ManualRequestKey, ct);
            return existing is null ? Fail<ManualAttendanceEventResult>("conflict", "Concurrent attendance intake changed. Retry using the same ManualRequestKey.") : Replay(existing, employeeId, r, utc);
        }
    }
    public async Task<ServiceResult<AttendanceEventDto>> EventAsync(Guid employeeId, Guid eventId, CancellationToken ct)
    {
        var e = await db.AttendanceEvents.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.AttendanceEventId == eventId, ct);
        return e is null ? Fail<AttendanceEventDto>("not_found", "Attendance event was not found for this employee.") : ServiceResult<AttendanceEventDto>.Success(Dto(e));
    }
    public async Task<ServiceResult<PagedResult<AttendanceEventDto>>> EventsAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken ct)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct)) return Fail<PagedResult<AttendanceEventDto>>("not_found", "Employee was not found.");
        if (fromDate > toDate || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return Fail<PagedResult<AttendanceEventDto>>("validation", "Valid inclusive dates, page >= 1 and pageSize 1–100 are required.");
        var query = db.AttendanceEvents.AsNoTracking().Where(x => x.EmployeeId == employeeId);
        if (fromDate.HasValue) query = query.Where(x => x.BusinessDate >= fromDate);
        if (toDate.HasValue) query = query.Where(x => x.BusinessDate <= toDate);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.BusinessDate).ThenBy(x => x.OccurredAtUtc).ThenBy(x => x.AttendanceEventId)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(e => new AttendanceEventDto(e.AttendanceEventId, e.EmployeeId,
                DateTime.SpecifyKind(e.OccurredAtUtc, DateTimeKind.Utc), e.BusinessDate, e.BusinessTimeZone, e.Direction, e.Source,
                e.SourceKey, e.ExternalEventId, e.ManualRequestKey, e.OriginalSourceTimestamp, DateTime.SpecifyKind(e.ReceivedAtUtc, DateTimeKind.Utc),
                e.Reason, e.ActorId, e.EmployeeWasInactive, e.EmploymentReadiness)).ToListAsync(ct);
        return ServiceResult<PagedResult<AttendanceEventDto>>.Success(new(items, page, pageSize, total));
    }
    public async Task<ServiceResult<AttendanceExpectedWorkDto>> ExpectedWorkAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var employee = await EmploymentIntegrity.LockAsync(db, employeeId, ct);
        if (employee is null) return Fail<AttendanceExpectedWorkDto>("not_found", "Employee was not found.");
        var employment = await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == employeeId).Where(EmploymentIntegrity.EffectiveOn(date)).Take(2).ToListAsync(ct);
        var assignments = await db.Set<EmployeeWorkCalendarAssignment>().AsNoTracking().Where(x => x.EmployeeId == employeeId && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).Take(2).ToListAsync(ct);
        var calendars = new List<WorkCalendar>();
        foreach (var id in assignments.Select(x => x.WorkCalendarId).Distinct().OrderBy(x => x))
        {
            var c = await db.WorkCalendars.FromSqlInterpolated($"SELECT * FROM [WorkCalendars] WITH (UPDLOCK) WHERE [Id] = {id}").AsNoTracking().SingleOrDefaultAsync(ct);
            if (c is not null) calendars.Add(c);
        }
        var ids = calendars.Select(x => x.Id).ToArray();
        var overrides = await db.Set<WorkCalendarDateOverride>().AsNoTracking().Where(x => ids.Contains(x.WorkCalendarId) && x.Date == date).ToListAsync(ct);
        var overrideIds = overrides.Select(x => x.Id).ToArray();
        var result = AttendanceFoundationResolver.Resolve(employeeId, employee.IsActive, date, employment, assignments, calendars,
            await db.Set<WorkCalendarWeeklyInterval>().AsNoTracking().Where(x => ids.Contains(x.WorkCalendarId) && x.DayOfWeek == date.DayOfWeek).ToListAsync(ct), overrides,
            await db.Set<WorkCalendarOverrideInterval>().AsNoTracking().Where(x => overrideIds.Contains(x.WorkCalendarDateOverrideId)).ToListAsync(ct));
        await tx.CommitAsync(ct);
        return ServiceResult<AttendanceExpectedWorkDto>.Success(result);
    }
}
