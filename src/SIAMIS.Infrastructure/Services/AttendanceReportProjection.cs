using System.Text.Json;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using static SIAMIS.Infrastructure.Services.AttendanceReviewSources;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Read-only status/privacy projection. All financial and interval calculations remain in D9C/D9D.</summary>
public static class AttendanceReportProjection
{
    public static AttendanceReportFacts Facts(AttendanceDayDto c, bool confirmed) => new()
    {
        Readiness=c.Readiness, ScheduleKind=c.ExpectedWork.ScheduleKind, CoveragePartitionAvailable=c.CoveragePartitionAvailable,
        ScheduledIntervals=c.ScheduledIntervals, Events=c.Events.Select(e=>new AttendanceReportEvent(e.AttendanceEventId,e.OccurredAtUtc,e.Direction,e.Source)).ToArray(),
        ApprovedLeaves=c.ApprovedLeaves.Select(l=>new AttendanceReportLeave(l.LeaveId,l.IsPaid,l.Date.ChargedIntervals.Select(i=>new AttendanceLeaveWindow(i.StartTime,i.EndTime)).ToArray())).ToArray(),
        FirstObservedInUtc=c.Events.Where(e=>e.Direction=="In").Select(e=>(DateTime?)e.OccurredAtUtc).Min(),
        LastObservedOutUtc=c.Events.Where(e=>e.Direction=="Out").Select(e=>(DateTime?)e.OccurredAtUtc).Max(),
        ExpectedArrivalUtc=c.ExpectedArrivalUtc, RawStartVarianceTicks=c.RawStartVarianceTicks, RawStartVarianceMilliseconds=c.RawStartVarianceMilliseconds,
        IsLate=c.IsLateUnderCurrentPolicy, PotentialAbsence=c.PotentialAbsence, IsConfirmedAbsent=confirmed,
        LeaveState=c.ApprovedLeaves.Count==0 ? AttendanceLeaveState.None : c.ApprovedLeaves.All(l=>l.IsPaid) ? AttendanceLeaveState.Paid : c.ApprovedLeaves.All(l=>!l.IsPaid) ? AttendanceLeaveState.Unpaid : AttendanceLeaveState.Mixed,
        LeaveExtent=c.ApprovedLeaves.Count==0 ? "None" : !c.CoveragePartitionAvailable ? "Unknown" : AttendanceDayCalculator.Ticks(c.ApprovedLeaveCoveredIntervals)==AttendanceDayCalculator.Ticks(c.ScheduledIntervals) ? "Full" : "Partial",
        ObservedPresenceMilliseconds=c.ObservedPresenceMilliseconds, ScheduledMilliseconds=c.ScheduledMilliseconds,
        PresenceCoveredScheduledMilliseconds=c.PresenceCoveredScheduledMilliseconds, ApprovedLeaveCoveredScheduledMilliseconds=c.ApprovedLeaveCoveredScheduledMilliseconds,
        PaidLeaveCoveredMilliseconds=c.PaidLeaveCoveredMilliseconds, UnpaidLeaveCoveredMilliseconds=c.UnpaidLeaveCoveredMilliseconds,
        UnexplainedScheduledMilliseconds=c.UnexplainedScheduledMilliseconds, CoverageTruncationResidualMilliseconds=c.CoverageTruncationResidualMilliseconds,
        CalculationContractVersion=c.CalculationContractVersion, GracePolicy=c.CurrentGracePolicy, ClockInGraceMinutes=c.ClockInGraceMinutes
    };

    public static AttendanceReportRow Row(Employee employee, AttendanceDayDto raw, IReadOnlyList<AttendanceReviewAction> actions,
        FinalizedAttendanceRevision? latest, string? reviewState, IReadOnlyDictionary<Guid,string> leaveStatuses,
        IReadOnlyDictionary<Guid,string> departments, IReadOnlyDictionary<Guid,string> designations, DateTime now)
    {
        var c=Calculate(raw,actions); var sources=Sources(raw,actions); var hash=Fingerprint(sources);
        bool reopened=latest is not null && actions.Any(a=>a.Action=="Reopened"&&a.FinalizedRevisionId==latest.Id);
        bool stale=latest is not null && latest.SourceFingerprint!=hash, invalid=false;
        IReadOnlyList<AttendanceDayFinding> changes=[]; AttendanceFinalizedSnapshot? frozen=null;
        if(latest is not null)
        {
            try
            {
                frozen=Parse<AttendanceFinalizedSnapshot>(latest.SnapshotJson);
                var storedSources=Parse<AttendanceSourceSet>(latest.SourcesJson);
                if(frozen.Version!=1 || frozen.Calculation.EmployeeId!=employee.EmployeeId || frozen.Calculation.BusinessDate!=raw.BusinessDate
                    || !frozen.Calculation.CoveragePartitionAvailable || frozen.IsConfirmedAbsent!=latest.IsConfirmedAbsent || frozen.Calculation.IsLateUnderCurrentPolicy!=latest.IsLate
                    || frozen.Calculation.ScheduledMilliseconds!=latest.ScheduledMilliseconds
                    || frozen.Calculation.PresenceCoveredScheduledMilliseconds!=latest.PresenceCoveredScheduledMilliseconds
                    || frozen.Calculation.ApprovedLeaveCoveredScheduledMilliseconds!=latest.ApprovedLeaveCoveredScheduledMilliseconds
                    || frozen.Calculation.UnexplainedScheduledMilliseconds!=latest.UnexplainedScheduledMilliseconds
                    || frozen.Calculation.CoverageTruncationResidualMilliseconds!=latest.CoverageTruncationResidualMilliseconds
                    || storedSources.EventIds is null || storedSources.LeaveIds is null || storedSources.Work is null || storedSources.Evidence is null
                    || storedSources.Leave is null || storedSources.Decisions is null || storedSources.Policy is null || storedSources.Integrity is null
                    || Fingerprint(storedSources)!=latest.SourceFingerprint) throw new JsonException("Invalid historical attendance snapshot.");
                if(stale) changes=Changes(storedSources,sources,leaveStatuses);
            }
            catch(Exception e) when(e is JsonException or NullReferenceException or InvalidOperationException)
            {
                invalid=true; frozen=null;
                changes=[new("FinalizedSnapshotInvalid","Stored finalized evidence cannot be safely used as official attendance; integrity review is required.",[latest.Id])];
            }
        }
        bool validated=latest is not null&&!stale&&!reopened&&!invalid;
        bool confirmed=Confirmed(c,hash,actions);
        var work=c.ExpectedWork.Readiness!="Ready" ? AttendanceWorkState.ConfigurationRequired : c.ScheduledMilliseconds>0 ? AttendanceWorkState.Scheduled : AttendanceWorkState.NotScheduled;
        var today=AttendanceFoundationResolver.BusinessDate(now);
        string relation=raw.BusinessDate<today ? "Past" : raw.BusinessDate==today ? "Today" : "Future";
        bool unfinalized=relation=="Past"&&work==AttendanceWorkState.Scheduled&&!validated;
        bool needsReview=stale||reopened||invalid||c.Findings.Any(f=>f.Code!="PotentialAbsence"||!confirmed);
        bool ready=relation=="Past"&&!validated&&!invalid&&(latest is null||reopened)&&CanFinalize(c,confirmed);
        var attention=new List<string>();
        if(stale)attention.Add("StaleFinalization"); if(reopened)attention.Add("Reopened"); if(needsReview)attention.Add("RequiresReview");
        if(work==AttendanceWorkState.ConfigurationRequired)attention.Add("ConfigurationRequired"); if(unfinalized)attention.Add("UnfinalizedPastDay"); if(ready)attention.Add("ReadyToFinalize");
        var timing=work==AttendanceWorkState.NotScheduled||confirmed||c.CoveragePartitionAvailable&&c.ApprovedLeaveCoveredScheduledMilliseconds==c.ScheduledMilliseconds
            ? AttendanceTimingState.NotApplicable : c.IsLateUnderCurrentPolicy is null ? AttendanceTimingState.Unknown : c.IsLateUnderCurrentPolicy.Value ? AttendanceTimingState.Late : AttendanceTimingState.OnTime;
        string scheduleWindow=work==AttendanceWorkState.ConfigurationRequired ? "Unknown" : work==AttendanceWorkState.NotScheduled ? "NotScheduled"
            : now<c.ScheduledIntervals[0].StartUtc ? "BeforeStart" : now>=c.ScheduledIntervals[^1].EndUtc ? "AfterEnd"
            : c.ScheduledIntervals.Any(i=>now>=i.StartUtc&&now<i.EndUtc) ? "InScheduledInterval" : "UnscheduledGap";
        string arrivalWindow=!c.ExpectedArrivalUtc.HasValue ? c.CoveragePartitionAvailable&&c.ApprovedLeaveCoveredScheduledMilliseconds==c.ScheduledMilliseconds ? "NotApplicable" : "Unknown"
            : now<c.ExpectedArrivalUtc.Value ? "BeforeExpectedArrival" : now.Ticks<=c.ExpectedArrivalUtc.Value.Ticks+c.ClockInGraceMinutes*TimeSpan.TicksPerMinute ? "WithinGrace" : "AfterGrace";
        var emp=c.ExpectedWork.Employment;
        return new()
        {
            EmployeeId=employee.EmployeeId,EmployeeNumber=employee.EmployeeNumber,DisplayName=string.Join(" ",new[]{employee.FirstName,employee.MiddleName,employee.LastName}.Where(n=>!string.IsNullOrWhiteSpace(n))),
            DepartmentId=emp?.DepartmentId,DepartmentName=emp?.DepartmentId is Guid d ? departments.GetValueOrDefault(d) : null,
            DesignationId=emp?.DesignationId,DesignationName=emp?.DesignationId is Guid j ? designations.GetValueOrDefault(j) : null,
            BusinessDate=raw.BusinessDate,WorkState=work,TimingState=timing,RecordState=invalid ? AttendanceRecordState.SnapshotInvalid : reopened ? AttendanceRecordState.Reopened
                : stale ? AttendanceRecordState.Stale : validated ? AttendanceRecordState.Finalized : unfinalized ? AttendanceRecordState.UnfinalizedPastDay : AttendanceRecordState.Live,
            DayRelation=relation,ScheduleWindow=scheduleWindow,ArrivalWindow=arrivalWindow,RequiresReview=needsReview,UnfinalizedHistoricalWorkingDay=unfinalized,ReadyToFinalize=ready,
            ReviewCaseState=reviewState,LatestHistoricalRevisionId=latest?.Id,LatestHistoricalRevision=latest?.Revision,
            FinalizedAtUtc=latest is null ? null : DateTime.SpecifyKind(latest.FinalizedAtUtc,DateTimeKind.Utc),IsStale=stale,RequiresReopen=stale&&!reopened,
            IsReopened=reopened,IsCurrentlyValidated=validated,Findings=c.Findings,ChangedSources=changes,AttentionCategories=attention,
            Live=Facts(c,confirmed),Official=validated ? Facts(frozen!.Calculation,frozen.IsConfirmedAbsent) : null
        };
    }
    public static bool Matches(AttendanceReportRow r,AttendanceReportFilter f)
        => (!f.Scheduled.HasValue||r.WorkState==(f.Scheduled.Value ? AttendanceWorkState.Scheduled : AttendanceWorkState.NotScheduled))
        &&(!f.Late.HasValue||r.Live.IsLate==f.Late) &&(!f.HasApprovedLeave.HasValue||(r.Live.ApprovedLeaves.Count>0)==f.HasApprovedLeave)
        &&(!f.ConfirmedAbsent.HasValue||r.Live.IsConfirmedAbsent==f.ConfirmedAbsent) &&(!f.RequiresReview.HasValue||r.RequiresReview==f.RequiresReview)
        &&(!f.IsStale.HasValue||r.IsStale==f.IsStale) &&(!f.Unfinalized.HasValue||(!r.IsCurrentlyValidated)==f.Unfinalized)
        &&(!f.RecordState.HasValue||r.RecordState==f.RecordState);
    public static AttendanceReportCounts Counts(IReadOnlyList<AttendanceReportRow> rows) => new(rows.Select(r=>r.EmployeeId).Distinct().Count(),rows.Count,
        rows.Count(r=>r.WorkState==AttendanceWorkState.Scheduled),rows.Count(r=>r.WorkState==AttendanceWorkState.NotScheduled),
        rows.Count(r=>r.TimingState==AttendanceTimingState.OnTime),rows.Count(r=>r.TimingState==AttendanceTimingState.Late),rows.Count(r=>r.Live.ApprovedLeaves.Count>0),
        rows.Count(r=>r.Live.IsConfirmedAbsent),rows.Count(r=>r.Live.PotentialAbsence&&!r.Live.IsConfirmedAbsent),rows.Count(r=>r.RequiresReview),
        rows.Count(r=>r.IsCurrentlyValidated),rows.Count(r=>r.IsStale),rows.Count(r=>r.WorkState==AttendanceWorkState.ConfigurationRequired),rows.Count(r=>r.IsReopened),
        rows.Count(r=>r.UnfinalizedHistoricalWorkingDay),rows.Count(r=>r.ReadyToFinalize));
    public static AttendanceSummaryDto Summary(Guid employee,DateOnly from,DateOnly to,DateTime now,IReadOnlyList<AttendanceReportRow> rows)
    {
        var official=rows.Where(r=>r.IsCurrentlyValidated&&r.Official is not null).Select(r=>r.Official!).ToArray();
        int missing=rows.Count(r=>r.WorkState==AttendanceWorkState.Scheduled&&!r.IsCurrentlyValidated),config=rows.Count(r=>r.WorkState==AttendanceWorkState.ConfigurationRequired);
        long Total(Func<AttendanceReportFacts,long?> value)=>official.Sum(f=>value(f)??0);
        return new()
        {
            EmployeeId=employee,From=from,To=to,GeneratedAtUtc=now,RequestedDates=to.DayNumber-from.DayNumber+1,EffectiveEmploymentDates=rows.Count,
            IsComplete=missing==0&&config==0&&rows.All(r=>!r.IsStale&&!r.IsReopened&&r.RecordState!=AttendanceRecordState.SnapshotInvalid),
            ScheduledWorkingDays=rows.Count(r=>r.WorkState==AttendanceWorkState.Scheduled),FinalizedWorkingDays=official.Count(f=>f.ScheduledMilliseconds>0),
            UnfinalizedWorkingDays=missing,StaleFinalizedDays=rows.Count(r=>r.IsStale),ReopenedDays=rows.Count(r=>r.IsReopened),ConfigurationRequiredDays=config,
            RequiresReviewDays=rows.Count(r=>r.RequiresReview),OnTimeDays=official.Count(f=>f.IsLate==false&&f.ScheduledMilliseconds>0),LateDays=official.Count(f=>f.IsLate==true&&f.ScheduledMilliseconds>0),
            ConfirmedAbsenceDays=official.Count(f=>f.IsConfirmedAbsent),PaidLeaveDates=official.Count(f=>f.PaidLeaveCoveredMilliseconds>0),UnpaidLeaveDates=official.Count(f=>f.UnpaidLeaveCoveredMilliseconds>0),
            PartialLeaveDates=official.Count(f=>f.LeaveExtent=="Partial"),NonWorkingActivityDates=official.Count(f=>f.ScheduledMilliseconds==0&&f.Events.Count>0),
            ScheduledMilliseconds=Total(f=>f.ScheduledMilliseconds),PresenceCoveredScheduledMilliseconds=Total(f=>f.PresenceCoveredScheduledMilliseconds),
            ApprovedLeaveCoveredScheduledMilliseconds=Total(f=>f.ApprovedLeaveCoveredScheduledMilliseconds),PaidLeaveCoveredMilliseconds=Total(f=>f.PaidLeaveCoveredMilliseconds),
            UnpaidLeaveCoveredMilliseconds=Total(f=>f.UnpaidLeaveCoveredMilliseconds),UnexplainedScheduledMilliseconds=Total(f=>f.UnexplainedScheduledMilliseconds),
            CoverageTruncationResidualMilliseconds=Total(f=>f.CoverageTruncationResidualMilliseconds)
        };
    }
}
