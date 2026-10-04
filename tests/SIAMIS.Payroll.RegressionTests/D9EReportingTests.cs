using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Services;

internal static class D9EReportingTests
{
    public static void Run(Action<bool,string> check)
    {
        var date=new DateOnly(2030,1,7);var emp=new Employee{EmployeeNumber="REPORT",FirstName="Test",LastName="Reporting",IsActive=false};
        var work=new AttendanceExpectedWorkDto(emp.EmployeeId,date,"Asia/Bangkok",false,"Ready",null,new(Guid.NewGuid(),date.AddDays(-1),null,null,null,null,null,null),Guid.NewGuid(),Guid.NewGuid(),"CODE","Name",null,"Weekly",[new(Guid.NewGuid(),new(7,30),new(16,0))]);
        DateTime T(int h,int m,long ticks=0)=>date.ToDateTime(new(h,m),DateTimeKind.Utc).AddHours(-7).AddTicks(ticks);
        AttendanceEventDto E(string d,DateTime t)=>new(Guid.NewGuid(),emp.EmployeeId,t,date,"Asia/Bangkok",d,"Device","private","private",null,null,T(18,0),"private medical reason",null,false,"Ready");
        AttendanceDayDto C(AttendanceEventDto[] events,AttendanceExpectedWorkDto? w=null)=>AttendanceDayCalculator.Calculate(w??work,events,[],[],T(18,0));
        AttendanceReportRow R(AttendanceDayDto c,FinalizedAttendanceRevision? f=null,AttendanceReviewAction[]? actions=null,DateTime? now=null)=>AttendanceReportProjection.Row(emp,c,actions??[],f,null,new Dictionary<Guid,string>(),new Dictionary<Guid,string>(),new Dictionary<Guid,string>(),now??T(18,0).AddDays(1));
        FinalizedAttendanceRevision F(AttendanceDayDto c,int revision=1,bool confirmed=false)
        {
            var sources=AttendanceReviewSources.Sources(c,[]);
            return new(){EmployeeId=emp.EmployeeId,BusinessDate=date,Revision=revision,IsConfirmedAbsent=confirmed,IsLate=c.IsLateUnderCurrentPolicy,
                ScheduledMilliseconds=c.ScheduledMilliseconds!.Value,PresenceCoveredScheduledMilliseconds=c.PresenceCoveredScheduledMilliseconds!.Value,
                ApprovedLeaveCoveredScheduledMilliseconds=c.ApprovedLeaveCoveredScheduledMilliseconds!.Value,UnexplainedScheduledMilliseconds=c.UnexplainedScheduledMilliseconds!.Value,
                CoverageTruncationResidualMilliseconds=c.CoverageTruncationResidualMilliseconds!.Value,SourceFingerprint=AttendanceReviewSources.Fingerprint(sources),SourcesJson=AttendanceReviewSources.Serialize(sources),
                SnapshotJson=AttendanceReviewSources.Serialize(new AttendanceFinalizedSnapshot(1,c,c.Events,[],confirmed))};
        }
        var pair=new[]{E("In",T(7,30)),E("Out",T(16,0))};var clean=C(pair);var live=R(clean);
        check(live.RecordState==AttendanceRecordState.UnfinalizedPastDay&&live.ReadyToFinalize&&live.Official is null,"D9E unfinalized ready past day explicit");
        var final=F(clean);var validated=R(clean,final);
        check(validated.IsCurrentlyValidated&&validated.RecordState==AttendanceRecordState.Finalized&&validated.Official is not null,"D9E validated frozen official facts");
        check(validated.CurrentPresence=="Unknown"&&live.CurrentPresence=="Unknown","D9E no invented inside state");
        var changed=R(C([pair[0],pair[1],E("In",T(17,0))]),final);
        check(changed.IsStale&&changed.RequiresReopen&&changed.Official is null&&changed.ChangedSources.Any(f=>f.Code=="AttendanceEvidenceChanged"),"D9E stale history not official");
        var reopened=R(clean,final,[new(){Action="Reopened",Sequence=2,FinalizedRevisionId=final.Id}]);
        check(reopened.IsReopened&&!reopened.IsCurrentlyValidated&&reopened.Official is null,"D9E reopen does not retain official truth");
        var absent=C([]);var ah=AttendanceReviewSources.Fingerprint(AttendanceReviewSources.Sources(absent,[]));
        var confirmed=new AttendanceReviewAction{Action="AbsenceConfirmed",Sequence=1,SourceFingerprint=ah};
        var absence=R(absent,F(absent,confirmed:true),[confirmed]);
        check(absence.Official!.IsConfirmedAbsent&&!absence.RequiresReview,"D9E confirmed absence distinct from provisional finding");
        var before=R(absent,now:T(7,0));check(before.DayRelation=="Today"&&before.ScheduleWindow=="BeforeStart"&&before.Live.PotentialAbsence&&!before.Live.IsConfirmedAbsent,"D9E before shift never automatic confirmed absence");
        foreach(long tick in new[]{0L,1L})
        {
            var a=R(absent,now:T(7,35,tick));check(a.ArrivalWindow==(tick==0 ? "WithinGrace" : "AfterGrace"),"D9E exact arrival window "+tick);
            var c=C([E("In",T(7,35,tick)),pair[1]]);var r=R(c,F(c));
            check(r.Official!.IsLate==(tick==1)&&r.Official.RawStartVarianceTicks==3000000000+tick,"D9E frozen grace/variance "+tick);
        }
        var residue=C([E("In",T(7,30,1)),pair[1]]);var rr=R(residue,F(residue));
        check(rr.Official!.CoverageTruncationResidualMilliseconds==1&&rr.Official.UnexplainedScheduledMilliseconds==0,"D9E residual remains precision only");
        var nonwork=C(pair,work with{Intervals=[],ScheduleKind="RestDay"});var nr=R(nonwork,F(nonwork));
        check(nr.WorkState==AttendanceWorkState.NotScheduled&&nr.TimingState==AttendanceTimingState.NotApplicable&&!nr.Live.PotentialAbsence,"D9E non-working activity distinct");
        var config=R(C([],work with{Readiness="WorkCalendarNotConfigured",Intervals=[]}));
        check(config.WorkState==AttendanceWorkState.ConfigurationRequired&&config.Live.ScheduledMilliseconds is null,"D9E missing configuration is not non-working");
        check(!AttendanceReportProjection.Matches(config,new(){Scheduled=false})&&!AttendanceReportProjection.Matches(config,new(){Scheduled=true}),"D9E unknown schedule matches neither scheduled boolean value");
        var incomplete=R(C([pair[0]]));check(incomplete.TimingState==AttendanceTimingState.Unknown&&incomplete.Live.IsLate is null&&incomplete.RequiresReview,"D9E incomplete evidence does not invent timing");
        var frozenPolicy=AttendanceReviewSources.Parse<AttendanceFinalizedSnapshot>(final.SnapshotJson);
        var bad=F(clean);bad.SnapshotJson=AttendanceReviewSources.Serialize(frozenPolicy with{Version=99});var br=R(clean,bad);
        check(br.RecordState==AttendanceRecordState.SnapshotInvalid&&br.Official is null&&br.RequiresReview,"D9E unknown frozen contract excluded without dashboard failure");
        var sum=AttendanceReportProjection.Summary(emp.EmployeeId,date,date,T(18,0),[validated,changed,reopened,live,config,absence]);
        check(!sum.IsComplete&&sum.FinalizedWorkingDays==2&&sum.ScheduledMilliseconds==61200000&&sum.ConfirmedAbsenceDays==1,"D9E official totals never mix live/stale/reopened");
        check(sum.StaleFinalizedDays==1&&sum.ReopenedDays==1&&sum.ConfigurationRequiredDays==1&&sum.UnfinalizedWorkingDays==3,"D9E completeness exposes missing states");
        var large=AttendanceReportProjection.Summary(emp.EmployeeId,date,date.AddDays(365),T(18,0),Enumerable.Repeat(validated,366).ToArray());
        check(large.ScheduledMilliseconds==30600000L*366&&large.ScheduledMilliseconds>int.MaxValue,"D9E wide duration aggregate");
        var rs=AttendanceReportProjection.Summary(emp.EmployeeId,date,date,T(18,0),[rr]);
        check(rs.ScheduledMilliseconds==rs.PresenceCoveredScheduledMilliseconds+rs.ApprovedLeaveCoveredScheduledMilliseconds+rs.UnexplainedScheduledMilliseconds+rs.CoverageTruncationResidualMilliseconds,"D9E summary aggregate residual identity");
        var counts=AttendanceReportProjection.Counts([validated,live]);check(counts.EffectiveEmployees==1&&counts.EmployeeDates==2&&counts.Scheduled==2,"D9E queue distinct staff vs overlapping date counts");
        check(AttendanceReportProjection.Matches(changed,new(){IsStale=true})&&!AttendanceReportProjection.Matches(validated,new(){IsStale=true}),"D9E typed stale filter");
        check(AttendanceReportingService.ValidateRange(null,date,366) is not null&&AttendanceReportingService.ValidateRange(date,date.AddDays(366),366) is not null,"D9E required/bounded ranges");
        check(AttendanceReportingService.ValidateRange(date,date.AddDays(365),366) is null,"D9E inclusive maximum range");
        check(AttendanceReportingService.ValidateFilter(new(){Page=int.MaxValue}) is not null&&AttendanceReportingService.ValidateFilter(new(){RecordState=(AttendanceRecordState)99}) is not null,"D9E invalid paging/filter rejected");
        check(AttendanceReportingService.ValidateBudget(500,1) is null&&AttendanceReportingService.ValidateBudget(501,1) is not null,"D9E inclusive population ceiling");
        check(AttendanceReportingService.ValidateBudget(100,20) is null&&AttendanceReportingService.ValidateBudget(100,21) is not null,"D9E inclusive employee/date budget");
        check(AttendanceReportingService.ValidateBudget(1,366) is null,"D9E single employee maximum history fits budget");
        var json=AttendanceReviewSources.Serialize(validated);
        foreach(var secret in new[]{"private medical reason","externalEventId","sourceKey","reason","salary","bank","documentTypeId","requiredDocumentTypeIds","supportingDocumentPolicy","actorUserId"})
            check(!json.Contains(secret,StringComparison.OrdinalIgnoreCase),"D9E minimized DTO omits "+secret);
    }
}
