using System.Data;
using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Security;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeClockService(SIAMISDbContext db, ICurrentActor actor, TimeProvider time) : IEmployeeClockService
{
    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
    private bool Linked => actor.UserId.HasValue && actor.EmployeeId.HasValue && actor.HasCapability("SelfService");
    private async Task<bool> AccountAsync(bool locked, CancellationToken ct)
    {
        if (!Linked) return false;
        var user = locked ? await AccountLock.LockAsync(db, actor.UserId!.Value, actor.EmployeeId, ct)
            : await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor.UserId, ct);
        if (user is null || !user.IsActive || user.RequiresPasswordChange || user.EmployeeId != actor.EmployeeId) return false;
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == user.Id select r.Name!).ToListAsync(ct);
        return SecurityCapabilities.ForRoles(roles).Contains("SelfService");
    }
    public Task<ServiceResult<EmployeeClockResult>> ClockInAsync(EmployeeClockInRequest r, CancellationToken ct)
        => CommandAsync(r.RequestKey, r.WorkArrangement, null, true, ct);
    public Task<ServiceResult<EmployeeClockResult>> ClockOutAsync(EmployeeClockOutRequest r, CancellationToken ct)
        => CommandAsync(r.RequestKey, null, r.SessionId, false, ct);

    private async Task<ServiceResult<EmployeeClockResult>> CommandAsync(Guid? key, ClockWorkArrangement? arrangement, Guid? sessionId, bool clockIn, CancellationToken ct)
    {
        if (!Linked) return Fail<EmployeeClockResult>("forbidden", "Authenticated linked employee SelfService access is required.");
        if (key is null || key == Guid.Empty || (clockIn ? arrangement is null || !Enum.IsDefined(arrangement.Value) : sessionId is null || sessionId == Guid.Empty))
            return Fail<EmployeeClockResult>("validation", "A nonempty RequestKey and valid work arrangement (IN) or owned SessionId (OUT) are required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var employee = await EmploymentIntegrity.LockAsync(db, actor.EmployeeId!.Value, ct);
            if (employee is null || !await AccountAsync(true, ct)) return Fail<EmployeeClockResult>("forbidden", "The authenticated employee linkage/account is not eligible.");
            string sourceKey = $"EmployeeClock/{employee.EmployeeId:D}", requestKey = key.Value.ToString("D");
            var replay = await db.AttendanceEvents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKey == sourceKey && x.ExternalEventId == requestKey, ct);
            if (replay is not null)
            {
                var previous = await db.EmployeeClockSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employee.EmployeeId && (x.InEventId == replay.AttendanceEventId || x.OutEventId == replay.AttendanceEventId), ct);
                if (previous is null || replay.Source != "EmployeeClock" || replay.Direction != (clockIn ? "In" : "Out")
                    || (clockIn ? previous.WorkArrangement != arrangement.ToString() : previous.Id != sessionId))
                    return Fail<EmployeeClockResult>("conflict", "RequestKey identifies a different clock command.");
                var result = await ReadSessionAsync(previous.Id, ct);
                await tx.CommitAsync(ct);
                return ServiceResult<EmployeeClockResult>.Success(new(result!, true));
            }
            var utc = time.GetUtcNow().UtcDateTime;
            var date = AttendanceFoundationResolver.BusinessDate(utc);
            var employment = await db.EmploymentRecords.AsNoTracking().Where(x => x.EmployeeId == employee.EmployeeId).Where(EmploymentIntegrity.EffectiveOn(date)).Take(2).ToListAsync(ct);
            if (!employee.IsActive || AttendanceFoundationResolver.EmploymentReadiness(employment) != "Ready"
                || !employment[0].IsCurrent || employment[0].EndDate.HasValue || await EmploymentIntegrity.ContextAsync(db, employment[0], ct) is not null)
                return Fail<EmployeeClockResult>("validation", "Clocking requires an active employee and one valid current open employment with active non-terminal employment status.");
            var open = await db.EmployeeClockSessions.SingleOrDefaultAsync(x => x.EmployeeId == employee.EmployeeId && x.OutEventId == null, ct);
            if (clockIn && open is not null) return Fail<EmployeeClockResult>("conflict", "An open clock session already exists; its missing OUT requires completion or HR review.");
            if (!clockIn && (open is null || open.Id != sessionId)) return Fail<EmployeeClockResult>("not_found", "An open clock session was not found for this employee.");
            var last = await db.AttendanceEvents.AsNoTracking().Where(x => x.EmployeeId == employee.EmployeeId && x.Source == "EmployeeClock")
                .OrderByDescending(x => x.OccurredAtUtc).Select(x => (DateTime?)x.OccurredAtUtc).FirstOrDefaultAsync(ct);
            if (last.HasValue && utc <= last.Value) return Fail<EmployeeClockResult>("conflict", "Server clock has not advanced beyond the previous clock evidence. Retry with the same RequestKey.");
            var evidence = new AttendanceEvent { EmployeeId = employee.EmployeeId, Source = "EmployeeClock", Direction = clockIn ? "In" : "Out",
                SourceKey = sourceKey, ExternalEventId = requestKey, OccurredAtUtc = utc, ReceivedAtUtc = utc,
                BusinessDate = date, ActorId = actor.UserId, EmployeeWasInactive = false, EmploymentReadiness = "Ready" };
            db.AttendanceEvents.Add(evidence);
            var session = open;
            if (clockIn)
            {
                session = new EmployeeClockSession { EmployeeId = employee.EmployeeId, WorkArrangement = arrangement.ToString()!, InEventId = evidence.AttendanceEventId };
                db.EmployeeClockSessions.Add(session);
            }
            else session!.OutEventId = evidence.AttendanceEventId;
            await db.SaveChangesAsync(ct);
            var dto = await ReadSessionAsync(session!.Id, ct);
            await tx.CommitAsync(ct);
            return ServiceResult<EmployeeClockResult>.Success(new(dto!, false));
        }
        catch (Exception e) when (Concurrent(e))
        {
            db.ChangeTracker.Clear();
            return Fail<EmployeeClockResult>("conflict", "Concurrent clock/account state changed. Reload and retry using the same RequestKey.");
        }
    }
    private static bool Concurrent(Exception e) => e is DbUpdateConcurrencyException || e is SqlException { Number: 1205 or 2601 or 2627 }
        || e.InnerException is not null && Concurrent(e.InnerException);
    private sealed class ClockRow
    {
        public EmployeeClockSession Session { get; init; } = null!;
        public AttendanceEvent In { get; init; } = null!;
        public AttendanceEvent? Out { get; init; }
    }
    private static readonly Expression<Func<ClockRow, EmployeeClockSessionDto>> Projection = x => new(
        x.Session.Id, x.Session.WorkArrangement, x.Session.InEventId, DateTime.SpecifyKind(x.In.OccurredAtUtc, DateTimeKind.Utc), x.In.BusinessDate,
        x.Session.OutEventId, x.Out == null ? null : DateTime.SpecifyKind(x.Out.OccurredAtUtc, DateTimeKind.Utc),
        x.Out == null ? null : x.Out.BusinessDate, x.In.BusinessTimeZone, x.Session.OutEventId == null);
    private IQueryable<ClockRow> Query() =>
        from s in db.EmployeeClockSessions.AsNoTracking()
        join i in db.AttendanceEvents.AsNoTracking() on s.InEventId equals i.AttendanceEventId
        join o in db.AttendanceEvents.AsNoTracking() on s.OutEventId equals (Guid?)o.AttendanceEventId into outs
        from o in outs.DefaultIfEmpty()
        where s.EmployeeId == actor.EmployeeId
        select new ClockRow { Session = s, In = i, Out = o };
    private Task<EmployeeClockSessionDto?> ReadSessionAsync(Guid id, CancellationToken ct) => Query().Where(x => x.Session.Id == id).Select(Projection).SingleOrDefaultAsync(ct);
    public async Task<ServiceResult<EmployeeClockSessionDto?>> CurrentAsync(CancellationToken ct)
    {
        if (!Linked) return Fail<EmployeeClockSessionDto?>("forbidden", "Authenticated linked employee SelfService access is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AccountAsync(true, ct)) return Fail<EmployeeClockSessionDto?>("forbidden", "Authenticated linked employee SelfService access is required.");
        var result = await Query().Where(x => x.Session.OutEventId == null).Select(Projection).SingleOrDefaultAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<EmployeeClockSessionDto?>.Success(result);
    }
    public async Task<ServiceResult<PagedResult<EmployeeClockSessionDto>>> HistoryAsync(DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct)
    {
        if (!Linked) return Fail<PagedResult<EmployeeClockSessionDto>>("forbidden", "Authenticated linked employee SelfService access is required.");
        if (from > to || page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return Fail<PagedResult<EmployeeClockSessionDto>>("validation", "Valid inclusive opening dates, page >= 1 and pageSize 1–100 are required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await AccountAsync(true, ct)) return Fail<PagedResult<EmployeeClockSessionDto>>("forbidden", "Authenticated linked employee SelfService access is required.");
        var query = Query();
        if (from.HasValue) query = query.Where(x => x.In.BusinessDate >= from);
        if (to.HasValue) query = query.Where(x => x.In.BusinessDate <= to);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.In.OccurredAtUtc).ThenBy(x => x.Session.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(Projection).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<PagedResult<EmployeeClockSessionDto>>.Success(new(rows, page, pageSize, count));
    }
}
