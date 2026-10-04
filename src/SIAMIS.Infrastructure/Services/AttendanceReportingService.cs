using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Leave;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class AttendanceReportingService(SIAMISDbContext db,TimeProvider clock) : IAttendanceReportingService
{
    public const int MaximumEmployees=500,MaximumEmployeeDates=2000,MaximumSourceRows=20000;
    private sealed record Batch(DateTime Now,IReadOnlyList<AttendanceReportRow> Rows);
    private sealed class QueryLimitException(string message) : Exception(message);
    private static ServiceResult<T> Fail<T>(string code,string message)=>ServiceResult<T>.Fail(code,message);
    public static string? ValidateRange(DateOnly? from,DateOnly? to,int maximumDays)
        => !from.HasValue||!to.HasValue||from==DateOnly.MinValue||from>to||to.Value.DayNumber-from.Value.DayNumber+1>maximumDays
            ? $"From/To are required, From must be 0001-01-02 or later and <= To; maximum inclusive range is {maximumDays} days." : null;
    public static string? ValidateFilter(AttendanceReportFilter f)
        => f.Page<1||f.PageSize is <1 or >100||(long)(f.Page-1)*f.PageSize>int.MaxValue||f.DepartmentId==Guid.Empty||f.DesignationId==Guid.Empty
            ||f.RecordState.HasValue&&!Enum.IsDefined(f.RecordState.Value) ? "Valid filters, nonempty IDs, Page >= 1 and PageSize 1–100 are required." : null;
    public static string? ValidateBudget(int employees,int dates)
        => employees>MaximumEmployees||(long)employees*dates>MaximumEmployeeDates
            ? $"Maximum {MaximumEmployees} candidate employees and {MaximumEmployeeDates} employee/date calculations per request. Narrow the department/position or date range." : null;
    private IQueryable<Employee> Candidates(DateOnly from,DateOnly to,AttendanceReportFilter f)
        => db.Employees.AsNoTracking().Where(e=>db.EmploymentRecords.Where(EmploymentIntegrity.Overlapping(from,to)).Any(r=>r.EmployeeId==e.EmployeeId
            &&(!f.DepartmentId.HasValue||r.DepartmentId==f.DepartmentId)&&(!f.DesignationId.HasValue||r.DesignationId==f.DesignationId)));
    private async Task<List<T>> Bounded<T>(IQueryable<T> query,CancellationToken ct)
    {
        var rows=await query.Take(MaximumSourceRows+1).ToListAsync(ct);
        if(rows.Count>MaximumSourceRows)throw new QueryLimitException("Attendance source row budget exceeded; narrow the employee/department/date range.");
        return rows;
    }
    private async Task<ServiceResult<Batch>> Load(Guid? employeeId,DateOnly from,DateOnly to,AttendanceReportFilter filter,DateTime now,CancellationToken ct)
    {
        // Select a bounded candidate cohort before acquiring source locks; verify membership again inside the transaction.
        var ids=employeeId.HasValue ? await db.Employees.AsNoTracking().Where(e=>e.EmployeeId==employeeId).Select(e=>e.EmployeeId).ToArrayAsync(ct)
            : await Candidates(from,to,filter).OrderBy(e=>e.EmployeeId).Select(e=>e.EmployeeId).Take(MaximumEmployees+1).ToArrayAsync(ct);
        if(employeeId.HasValue&&ids.Length==0)return Fail<Batch>("not_found","Employee was not found.");
        var budget=ValidateBudget(ids.Length,to.DayNumber-from.DayNumber+1);
        if(budget is not null)return Fail<Batch>("validation",budget);
        if(ids.Length==0)return ServiceResult<Batch>.Success(new(now,[]));
        try
        {
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            string employeeIds=AttendanceReviewSources.Serialize(ids);
            // One ordered, single-worker PK scan acquires Employee update locks before any calendar/source locks.
            var employees=await db.Employees.FromSqlInterpolated($"SELECT * FROM [Employees] WITH (UPDLOCK, INDEX([PK_Employees])) WHERE [EmployeeId] IN (SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON({employeeIds})) ORDER BY [EmployeeId] OPTION (MAXDOP 1)").AsNoTracking().ToListAsync(ct);
            if(employees.Count!=ids.Length)return Fail<Batch>("conflict","Attendance population changed; retry the query.");
            if(!employeeId.HasValue)
            {
                var verified=await Candidates(from,to,filter).OrderBy(e=>e.EmployeeId).Select(e=>e.EmployeeId).Take(MaximumEmployees+1).ToArrayAsync(ct);
                if(!ids.ToHashSet().SetEquals(verified))return Fail<Batch>("conflict","Effective employee population changed; retry the query.");
            }
            var employment=await Bounded(db.EmploymentRecords.AsNoTracking().Where(r=>ids.Contains(r.EmployeeId)).Where(EmploymentIntegrity.Overlapping(from,to)).OrderBy(r=>r.EmploymentRecordId),ct);
            var assignments=await Bounded(db.Set<EmployeeWorkCalendarAssignment>().AsNoTracking().Where(a=>ids.Contains(a.EmployeeId)&&a.EffectiveFrom<=to&&(!a.EffectiveTo.HasValue||a.EffectiveTo>=from)).OrderBy(a=>a.Id),ct);
            var calendarIds=assignments.Select(a=>a.WorkCalendarId).Distinct().ToArray();
            string calendarJson=AttendanceReviewSources.Serialize(calendarIds);
            var calendars=await db.WorkCalendars.FromSqlInterpolated($"SELECT * FROM [WorkCalendars] WITH (UPDLOCK, INDEX([PK_WorkCalendars])) WHERE [Id] IN (SELECT CONVERT(uniqueidentifier,[value]) FROM OPENJSON({calendarJson})) ORDER BY [Id] OPTION (MAXDOP 1)").AsNoTracking().ToListAsync(ct);
            var weekly=await Bounded(db.Set<WorkCalendarWeeklyInterval>().AsNoTracking().Where(w=>calendarIds.Contains(w.WorkCalendarId)).OrderBy(w=>w.Id),ct);
            var overrides=await Bounded(db.Set<WorkCalendarDateOverride>().AsNoTracking().Where(o=>calendarIds.Contains(o.WorkCalendarId)&&o.Date>=from&&o.Date<=to).OrderBy(o=>o.Id),ct);
            var overrideIds=overrides.Select(o=>o.Id).ToArray();
            var replacement=await Bounded(db.Set<WorkCalendarOverrideInterval>().AsNoTracking().Where(i=>overrideIds.Contains(i.WorkCalendarDateOverrideId)).OrderBy(i=>i.Id),ct);
            var events=await Bounded(db.AttendanceEvents.AsNoTracking().Where(e=>ids.Contains(e.EmployeeId)&&e.BusinessDate>=from&&e.BusinessDate<=to).OrderBy(e=>e.AttendanceEventId),ct);
            // Load all statuses in the bounded range so cancellation findings can use actual current status without per-day queries.
            var leaves=await Bounded(db.EmployeeLeaves.AsNoTracking().Where(l=>ids.Contains(l.EmployeeId)&&l.StartDate<=to&&l.EndDate>=from).OrderBy(l=>l.LeaveId),ct);
            var approvedIds=leaves.Where(l=>l.Status=="Approved").Select(l=>l.LeaveId).ToArray();
            var allocations=await Bounded(db.Set<EmployeeLeaveAllocation>().AsNoTracking().Where(a=>approvedIds.Contains(a.EmployeeLeaveId)).OrderBy(a=>a.Id),ct);
            var actions=await Bounded(db.Set<AttendanceReviewAction>().AsNoTracking().Where(a=>ids.Contains(a.EmployeeId)&&a.BusinessDate>=from&&a.BusinessDate<=to)
                .OrderBy(a=>a.EmployeeId).ThenBy(a=>a.BusinessDate).ThenBy(a=>a.Sequence).Select(a=>new AttendanceReviewAction{Id=a.Id,EmployeeId=a.EmployeeId,BusinessDate=a.BusinessDate,Sequence=a.Sequence,Action=a.Action,AttendanceEventId=a.AttendanceEventId,FinalizedRevisionId=a.FinalizedRevisionId,SourceFingerprint=a.SourceFingerprint}),ct);
            var cases=await Bounded(db.Set<AttendanceReviewCase>().AsNoTracking().Where(c=>ids.Contains(c.EmployeeId)&&c.BusinessDate>=from&&c.BusinessDate<=to).OrderBy(c=>c.EmployeeId).ThenBy(c=>c.BusinessDate).Select(c=>new{c.EmployeeId,c.BusinessDate,c.State}),ct);
            var revisions=await Bounded(db.Set<FinalizedAttendanceRevision>().AsNoTracking().Where(r=>ids.Contains(r.EmployeeId)&&r.BusinessDate>=from&&r.BusinessDate<=to)
                .Where(r=>!db.Set<FinalizedAttendanceRevision>().Any(newer=>newer.EmployeeId==r.EmployeeId&&newer.BusinessDate==r.BusinessDate&&newer.Revision>r.Revision)).OrderBy(r=>r.EmployeeId).ThenBy(r=>r.BusinessDate),ct);
            var oldLeaveIds=revisions.SelectMany(r=>
            {
                try{return AttendanceReviewSources.Parse<AttendanceSourceSet>(r.SourcesJson).LeaveIds??[];}catch(System.Text.Json.JsonException){return Array.Empty<Guid>();}
            }).Except(leaves.Select(l=>l.LeaveId)).Distinct().ToArray();
            var statuses=leaves.ToDictionary(l=>l.LeaveId,l=>l.Status);
            if(oldLeaveIds.Length>0)
                foreach(var l in await db.EmployeeLeaves.AsNoTracking().Where(l=>ids.Contains(l.EmployeeId)&&oldLeaveIds.Contains(l.LeaveId)).Select(l=>new{l.LeaveId,l.Status}).ToListAsync(ct))statuses[l.LeaveId]=l.Status;
            var departments=await db.Departments.AsNoTracking().Where(d=>employment.Select(e=>e.DepartmentId).Contains(d.Id)).Select(d=>new{d.Id,d.Name}).ToDictionaryAsync(d=>d.Id,d=>d.Name,ct);
            var designations=await db.Designations.AsNoTracking().Where(d=>employment.Select(e=>e.DesignationId).Contains(d.Id)).Select(d=>new{d.Id,d.Name}).ToDictionaryAsync(d=>d.Id,d=>d.Name,ct);
            var empById=employment.ToLookup(e=>e.EmployeeId);var assignmentsById=assignments.ToLookup(a=>a.EmployeeId);
            var calendarLookup=calendars.ToDictionary(c=>c.Id);var weeklyLookup=weekly.ToLookup(w=>w.WorkCalendarId);
            var overrideLookup=overrides.ToLookup(o=>(o.WorkCalendarId,o.Date));var replacementLookup=replacement.ToLookup(i=>i.WorkCalendarDateOverrideId);
            var eventDays=events.ToLookup(e=>(e.EmployeeId,e.BusinessDate));var actionDays=actions.ToLookup(a=>(a.EmployeeId,a.BusinessDate));
            var caseDays=cases.ToDictionary(c=>(c.EmployeeId,c.BusinessDate),c=>c.State);var revisionDays=revisions.ToDictionary(r=>(r.EmployeeId,r.BusinessDate));
            var allocationLookup=allocations.ToLookup(a=>a.EmployeeLeaveId);
            var leaveReads=leaves.Where(l=>l.Status=="Approved").Select(l=>new{Header=l,Read=LeaveSnapshotIntegrity.Read(l,allocationLookup[l.LeaveId].ToArray())}).ToLookup(l=>l.Header.EmployeeId);
            var result=new List<AttendanceReportRow>();
            foreach(var employee in employees)
            for(int n=from.DayNumber;n<=to.DayNumber;n++)
            {
                var date=DateOnly.FromDayNumber(n);var effective=empById[employee.EmployeeId].Where(e=>EmploymentIntegrity.Start(e)<=date&&(!e.EndDate.HasValue||e.EndDate>=date)).ToArray();
                if(!effective.Any(e=>(!filter.DepartmentId.HasValue||e.DepartmentId==filter.DepartmentId)&&(!filter.DesignationId.HasValue||e.DesignationId==filter.DesignationId)))continue;
                var employeeAssignments=assignmentsById[employee.EmployeeId].ToArray();
                var dayCalendarIds=employeeAssignments.Where(a=>a.EffectiveFrom<=date&&(!a.EffectiveTo.HasValue||a.EffectiveTo>=date)).Select(a=>a.WorkCalendarId).Distinct().ToArray();
                var dayOverrides=dayCalendarIds.SelectMany(id=>overrideLookup[(id,date)]).ToArray();
                var work=AttendanceFoundationResolver.Resolve(employee.EmployeeId,employee.IsActive,date,effective,employeeAssignments,
                    dayCalendarIds.Where(calendarLookup.ContainsKey).Select(id=>calendarLookup[id]).ToArray(),dayCalendarIds.SelectMany(id=>weeklyLookup[id]).ToArray(),
                    dayOverrides,dayOverrides.SelectMany(o=>replacementLookup[o.Id]).ToArray());
                var es=eventDays[(employee.EmployeeId,date)].Select(e=>new AttendanceEventDto(e.AttendanceEventId,e.EmployeeId,DateTime.SpecifyKind(e.OccurredAtUtc,DateTimeKind.Utc),e.BusinessDate,e.BusinessTimeZone,e.Direction,e.Source,e.SourceKey,e.ExternalEventId,e.ManualRequestKey,e.OriginalSourceTimestamp,DateTime.SpecifyKind(e.ReceivedAtUtc,DateTimeKind.Utc),e.Reason,e.ActorId,e.EmployeeWasInactive,e.EmploymentReadiness)).ToArray();
                var ls=new List<AttendanceLeaveEvidence>();var findings=new List<AttendanceDayFinding>();
                foreach(var read in leaveReads[employee.EmployeeId].Where(l=>l.Header.StartDate<=date&&l.Header.EndDate>=date))
                {
                    if(!read.Read.IsSuccess)findings.Add(new("LeaveSnapshotInvalid",read.Read.Failure!.Message,[read.Header.LeaveId]));
                    else ls.Add(new(read.Header.LeaveId,"Approved",read.Read.Value!.Version,read.Read.Value.IsPaid!.Value,read.Read.Value.Dates.Single(d=>d.Date==date)));
                }
                var raw=AttendanceDayCalculator.Calculate(work,es,ls.OrderBy(l=>l.LeaveId).ToArray(),findings,now);
                result.Add(AttendanceReportProjection.Row(employee,raw,actionDays[(employee.EmployeeId,date)].OrderBy(a=>a.Sequence).ToArray(),revisionDays.GetValueOrDefault((employee.EmployeeId,date)),caseDays.GetValueOrDefault((employee.EmployeeId,date)),statuses,departments,designations,now));
            }
            await tx.CommitAsync(ct);return ServiceResult<Batch>.Success(new(now,result));
        }
        catch(QueryLimitException e){return Fail<Batch>("validation",e.Message);}
        catch(SqlException e) when(e.Number==1205){return Fail<Batch>("conflict","Concurrent attendance sources changed; retry the query.");}
    }
    private static PagedResult<AttendanceReportRow> Page(IReadOnlyList<AttendanceReportRow> rows,AttendanceReportFilter filter)
        => new(rows.Skip((filter.Page-1)*filter.PageSize).Take(filter.PageSize).ToArray(),filter.Page,filter.PageSize,rows.Count);
    public async Task<ServiceResult<AttendanceOverviewDto>> DailyAsync(DateOnly? date,AttendanceReportFilter f,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;var day=date??AttendanceFoundationResolver.BusinessDate(now);
        var error=ValidateRange(day,day,1)??ValidateFilter(f);if(error is not null)return Fail<AttendanceOverviewDto>("validation",error);
        var b=await Load(null,day,day,f,now,ct);if(!b.IsSuccess)return Fail<AttendanceOverviewDto>(b.Failure!.Code,b.Failure.Message);
        var rows=b.Value!.Rows.Where(r=>AttendanceReportProjection.Matches(r,f)).OrderBy(r=>r.EmployeeNumber,StringComparer.Ordinal).ThenBy(r=>r.EmployeeId).ToArray();
        return ServiceResult<AttendanceOverviewDto>.Success(new(day,AttendanceFoundationResolver.BusinessTimeZone,now,b.Value.Rows.Count,AttendanceReportProjection.Counts(rows),Page(rows,f)));
    }
    public async Task<ServiceResult<AttendanceHistoryDto>> HistoryAsync(Guid employeeId,AttendanceRangeQuery q,CancellationToken ct)
    {
        var error=ValidateRange(q.From,q.To,366);if(error is not null)return Fail<AttendanceHistoryDto>("validation",error);
        var now=clock.GetUtcNow().UtcDateTime;var b=await Load(employeeId,q.From!.Value,q.To!.Value,new(),now,ct);
        return !b.IsSuccess ? Fail<AttendanceHistoryDto>(b.Failure!.Code,b.Failure.Message) : ServiceResult<AttendanceHistoryDto>.Success(new(employeeId,q.From.Value,q.To.Value,AttendanceFoundationResolver.BusinessTimeZone,now,q.To.Value.DayNumber-q.From.Value.DayNumber+1,b.Value!.Rows.OrderBy(r=>r.BusinessDate).ToArray()));
    }
    public async Task<ServiceResult<AttendanceSummaryDto>> SummaryAsync(Guid employeeId,AttendanceRangeQuery q,CancellationToken ct)
    {
        var error=ValidateRange(q.From,q.To,366);if(error is not null)return Fail<AttendanceSummaryDto>("validation",error);
        var now=clock.GetUtcNow().UtcDateTime;var b=await Load(employeeId,q.From!.Value,q.To!.Value,new(),now,ct);
        return !b.IsSuccess ? Fail<AttendanceSummaryDto>(b.Failure!.Code,b.Failure.Message) : ServiceResult<AttendanceSummaryDto>.Success(AttendanceReportProjection.Summary(employeeId,q.From.Value,q.To.Value,now,b.Value!.Rows));
    }
    public async Task<ServiceResult<AttendanceAttentionQueueDto>> QueueAsync(AttendanceQueueQuery q,CancellationToken ct)
    {
        var error=ValidateRange(q.From,q.To,31)??ValidateFilter(q);if(error is not null)return Fail<AttendanceAttentionQueueDto>("validation",error);
        var now=clock.GetUtcNow().UtcDateTime;var b=await Load(null,q.From!.Value,q.To!.Value,q,now,ct);
        if(!b.IsSuccess)return Fail<AttendanceAttentionQueueDto>(b.Failure!.Code,b.Failure.Message);
        var rows=b.Value!.Rows.Where(r=>r.AttentionCategories.Count>0&&AttendanceReportProjection.Matches(r,q)).OrderBy(r=>r.BusinessDate).ThenBy(r=>r.EmployeeNumber,StringComparer.Ordinal).ThenBy(r=>r.EmployeeId).ToArray();
        return ServiceResult<AttendanceAttentionQueueDto>.Success(new(q.From.Value,q.To.Value,AttendanceFoundationResolver.BusinessTimeZone,now,b.Value.Rows.Count,AttendanceReportProjection.Counts(rows),Page(rows,q)));
    }
}
