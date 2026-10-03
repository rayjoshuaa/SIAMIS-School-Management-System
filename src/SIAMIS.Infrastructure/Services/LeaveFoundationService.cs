using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed partial class LeaveFoundationService(SIAMISDbContext db) : ILeaveFoundationService
{
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> Missing<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private static bool Unique(DbUpdateException e) => e.InnerException is SqlException { Number: 2601 or 2627 };
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private Task<bool> Exists(Guid employee, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employee, ct);
    private Task<WorkCalendar?> LockCalendar(Guid id, CancellationToken ct)
        => db.Set<WorkCalendar>().FromSqlInterpolated($"SELECT * FROM [WorkCalendars] WITH (UPDLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct);
    private static WorkCalendarDto CalendarDto(WorkCalendar x) => new(x.Id, x.Code, x.Name, x.Description, x.IsActive, x.IsDefault, Utc(x.CreatedAt), Utc(x.UpdatedAt));
    private static CalendarAssignmentDto AssignmentDto(EmployeeWorkCalendarAssignment x) => new(x.Id, x.EmployeeId, x.WorkCalendarId, x.EffectiveFrom, x.EffectiveTo);

    public async Task<IReadOnlyList<WorkCalendarDto>> CalendarsAsync(CancellationToken ct)
        => (await db.Set<WorkCalendar>().AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct)).Select(CalendarDto).ToArray();

    public async Task<ServiceResult<WorkCalendarDto>> SaveCalendarAsync(Guid? id, WorkCalendarRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || r.Code.Length > 50 || string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 150 || r.Description?.Length > 1000)
            return Invalid<WorkCalendarDto>("Code/Name are required and must respect their length limits.");
        if (r.IsDefault && !r.IsActive) return Invalid<WorkCalendarDto>("A default suggestion must be active.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Serialize default suggestions and creations, including the initially empty index range.
        var calendars = await db.Set<WorkCalendar>().FromSqlRaw("SELECT * FROM [WorkCalendars] WITH (UPDLOCK)").ToListAsync(ct);
        var x = id.HasValue ? calendars.SingleOrDefault(x => x.Id == id) : new WorkCalendar();
        if (x is null) return Missing<WorkCalendarDto>("Work calendar was not found.");
        if (r.IsDefault && calendars.Any(y => y.Id != x.Id && y.IsActive && y.IsDefault)) return Conflict<WorkCalendarDto>("An active default suggestion already exists.");
        x.Code = r.Code.Trim(); x.Name = r.Name.Trim(); x.Description = Clean(r.Description); x.IsActive = r.IsActive; x.IsDefault = r.IsDefault;
        if (!id.HasValue) db.Add(x);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (Unique(e)) { return Conflict<WorkCalendarDto>("Calendar code or active default suggestion already exists."); }
        return ServiceResult<WorkCalendarDto>.Success(CalendarDto(x));
    }

    public async Task<ServiceResult<IReadOnlyList<WeeklyIntervalDto>>> WeeklyAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Set<WorkCalendar>().AnyAsync(x => x.Id == id, ct)) return Missing<IReadOnlyList<WeeklyIntervalDto>>("Work calendar was not found.");
        return ServiceResult<IReadOnlyList<WeeklyIntervalDto>>.Success(await db.Set<WorkCalendarWeeklyInterval>().AsNoTracking().Where(x => x.WorkCalendarId == id)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).Select(x => new WeeklyIntervalDto(x.Id, x.DayOfWeek, x.StartTime, x.EndTime)).ToListAsync(ct));
    }
    private static bool ValidTimes(TimeOnly? start, TimeOnly? end)
        => start.HasValue && end.HasValue && start < end && start.Value.Ticks % TimeSpan.TicksPerMinute == 0 && end.Value.Ticks % TimeSpan.TicksPerMinute == 0;
    public async Task<ServiceResult<WeeklyIntervalDto>> AddWeeklyAsync(Guid id, WeeklyIntervalRequest r, CancellationToken ct)
    {
        if (!r.DayOfWeek.HasValue || !Enum.IsDefined(r.DayOfWeek.Value) || !ValidTimes(r.StartTime, r.EndTime)) return Invalid<WeeklyIntervalDto>("A valid weekday and whole-minute StartTime < EndTime are required; overnight intervals are not supported.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await LockCalendar(id, ct) is null) return Missing<WeeklyIntervalDto>("Work calendar was not found.");
        if (await db.Set<WorkCalendarWeeklyInterval>().AnyAsync(x => x.WorkCalendarId == id && x.DayOfWeek == r.DayOfWeek && x.StartTime < r.EndTime && x.EndTime > r.StartTime, ct))
            return Conflict<WeeklyIntervalDto>("Working intervals overlap.");
        var x = new WorkCalendarWeeklyInterval { WorkCalendarId = id, DayOfWeek = r.DayOfWeek.Value, StartTime = r.StartTime!.Value, EndTime = r.EndTime!.Value };
        db.Add(x); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<WeeklyIntervalDto>.Success(new(x.Id, x.DayOfWeek, x.StartTime, x.EndTime));
    }
    public async Task<ServiceResult<IReadOnlyList<CalendarOverrideDto>>> OverridesAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Set<WorkCalendar>().AnyAsync(x => x.Id == id, ct)) return Missing<IReadOnlyList<CalendarOverrideDto>>("Work calendar was not found.");
        var dates = await db.Set<WorkCalendarDateOverride>().AsNoTracking().Where(x => x.WorkCalendarId == id).OrderBy(x => x.Date).ToListAsync(ct);
        var intervals = await db.Set<WorkCalendarOverrideInterval>().AsNoTracking().Where(x => db.Set<WorkCalendarDateOverride>().Any(y => y.Id == x.WorkCalendarDateOverrideId && y.WorkCalendarId == id)).OrderBy(x => x.StartTime).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<CalendarOverrideDto>>.Success(dates.Select(x => new CalendarOverrideDto(x.Id, id, x.Date, x.OverrideType, x.Name, x.Description,
            intervals.Where(y => y.WorkCalendarDateOverrideId == x.Id).Select(y => new WorkIntervalDto(y.StartTime, y.EndTime)).ToArray())).ToArray());
    }
    public async Task<ServiceResult<CalendarOverrideDto>> AddOverrideAsync(Guid id, CalendarOverrideRequest r, CancellationToken ct)
    {
        if (!r.Date.HasValue || r.OverrideType is not ("PublicHoliday" or "SchoolHoliday" or "RestDay" or "ExceptionalWorkingDay") || r.Name?.Length > 150 || r.Description?.Length > 1000)
            return Invalid<CalendarOverrideDto>("A date and supported override category are required.");
        if (r.Intervals is null || (r.OverrideType == "ExceptionalWorkingDay" ? r.Intervals.Count == 0 : r.Intervals.Count != 0))
            return Invalid<CalendarOverrideDto>("ExceptionalWorkingDay requires replacement intervals; non-working overrides require none.");
        if (r.Intervals.Any(x => x is null || !ValidTimes(x.StartTime, x.EndTime))) return Invalid<CalendarOverrideDto>("Replacement intervals must use whole minutes and StartTime < EndTime.");
        var ordered = r.Intervals.OrderBy(x => x.StartTime).ToArray();
        if (ordered.Skip(1).Where((x, i) => x.StartTime < ordered[i].EndTime).Any()) return Invalid<CalendarOverrideDto>("Replacement intervals overlap.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await LockCalendar(id, ct) is null) return Missing<CalendarOverrideDto>("Work calendar was not found.");
        if (await db.Set<WorkCalendarDateOverride>().AnyAsync(x => x.WorkCalendarId == id && x.Date == r.Date, ct)) return Conflict<CalendarOverrideDto>("An override already exists for this date.");
        var x = new WorkCalendarDateOverride { WorkCalendarId = id, Date = r.Date.Value, OverrideType = r.OverrideType, Name = Clean(r.Name), Description = Clean(r.Description) };
        db.Add(x); foreach (var i in ordered) db.Add(new WorkCalendarOverrideInterval { WorkCalendarDateOverrideId = x.Id, StartTime = i.StartTime!.Value, EndTime = i.EndTime!.Value });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<CalendarOverrideDto>.Success(new(x.Id, id, x.Date, x.OverrideType, x.Name, x.Description, ordered.Select(i => new WorkIntervalDto(i.StartTime!.Value, i.EndTime!.Value)).ToArray()));
    }
    public async Task<ServiceResult<IReadOnlyList<CalendarAssignmentDto>>> AssignmentsAsync(Guid employee, CancellationToken ct)
    {
        if (!await Exists(employee, ct)) return Missing<IReadOnlyList<CalendarAssignmentDto>>("Employee was not found.");
        return ServiceResult<IReadOnlyList<CalendarAssignmentDto>>.Success((await db.Set<EmployeeWorkCalendarAssignment>().AsNoTracking().Where(x => x.EmployeeId == employee).OrderBy(x => x.EffectiveFrom).ToListAsync(ct)).Select(AssignmentDto).ToArray());
    }
    public async Task<ServiceResult<CalendarAssignmentDto>> AssignAsync(Guid employee, CalendarAssignmentRequest r, CancellationToken ct)
    {
        if (!r.WorkCalendarId.HasValue || !r.EffectiveFrom.HasValue || r.EffectiveTo < r.EffectiveFrom) return Invalid<CalendarAssignmentDto>("A calendar and valid inclusive effective dates are required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employee, ct) is null) return Missing<CalendarAssignmentDto>("Employee was not found.");
        if (await LockCalendar(r.WorkCalendarId.Value, ct) is not { IsActive: true }) return Invalid<CalendarAssignmentDto>("WorkCalendarId must reference an active calendar.");
        if (await db.Set<EmployeeWorkCalendarAssignment>().AnyAsync(x => x.EmployeeId == employee && (!x.EffectiveTo.HasValue || x.EffectiveTo >= r.EffectiveFrom) && (!r.EffectiveTo.HasValue || x.EffectiveFrom <= r.EffectiveTo), ct))
            return Conflict<CalendarAssignmentDto>("Effective calendar assignments overlap.");
        var x = new EmployeeWorkCalendarAssignment { EmployeeId = employee, WorkCalendarId = r.WorkCalendarId.Value, EffectiveFrom = r.EffectiveFrom.Value, EffectiveTo = r.EffectiveTo };
        db.Add(x); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<CalendarAssignmentDto>.Success(AssignmentDto(x));
    }
    public async Task<ServiceResult<CalendarResolutionDto>> ResolveCalendarAsync(Guid employee, DateOnly date, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employee, ct) is null) return Missing<CalendarResolutionDto>("Employee was not found.");
        var assignments = await db.Set<EmployeeWorkCalendarAssignment>().AsNoTracking().Where(x => x.EmployeeId == employee && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).Take(2).ToListAsync(ct);
        if (assignments.Count == 0) return Conflict<CalendarResolutionDto>("Work calendar not configured for the requested date.");
        if (assignments.Count != 1) return Conflict<CalendarResolutionDto>("Multiple effective work calendar assignments; calendar resolution is ambiguous.");
        var a = assignments[0];
        // IsDefault and current IsActive never reinterpret an existing historical assignment.
        if (await LockCalendar(a.WorkCalendarId, ct) is null) return Conflict<CalendarResolutionDto>("Assigned calendar is missing.");
        var o = await db.Set<WorkCalendarDateOverride>().AsNoTracking().SingleOrDefaultAsync(x => x.WorkCalendarId == a.WorkCalendarId && x.Date == date, ct);
        var intervals = o is not null
            ? await db.Set<WorkCalendarOverrideInterval>().AsNoTracking().Where(x => x.WorkCalendarDateOverrideId == o.Id).OrderBy(x => x.StartTime).Select(x => new WorkIntervalDto(x.StartTime, x.EndTime)).ToListAsync(ct)
            : await db.Set<WorkCalendarWeeklyInterval>().AsNoTracking().Where(x => x.WorkCalendarId == a.WorkCalendarId && x.DayOfWeek == date.DayOfWeek).OrderBy(x => x.StartTime).Select(x => new WorkIntervalDto(x.StartTime, x.EndTime)).ToListAsync(ct);
        if ((o is not null && (o.OverrideType == "ExceptionalWorkingDay" ? intervals.Count == 0 : intervals.Count != 0)) || intervals.Skip(1).Where((x, i) => x.StartTime < intervals[i].EndTime).Any())
            return Conflict<CalendarResolutionDto>("Calendar interval configuration is inconsistent.");
        var minutes = intervals.Sum(x => (int)(x.EndTime.ToTimeSpan() - x.StartTime.ToTimeSpan()).TotalMinutes);
        await tx.CommitAsync(ct);
        return ServiceResult<CalendarResolutionDto>.Success(new(a.Id, a.WorkCalendarId, date, o?.OverrideType, intervals, minutes));
    }
}
