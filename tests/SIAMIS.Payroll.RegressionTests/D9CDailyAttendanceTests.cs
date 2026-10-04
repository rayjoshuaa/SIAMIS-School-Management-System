using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Services;

internal static class D9CDailyAttendanceTests
{
    public static void Run(Action<bool, string> check)
    {
        var date = new DateOnly(2030, 1, 7); var employee = Guid.NewGuid(); var employment = Guid.NewGuid(); var assignment = Guid.NewGuid(); var calendar = Guid.NewGuid();
        AttendanceScheduleInterval S(int h, int m, int eh, int em) => new(Guid.NewGuid(), new(h, m), new(eh, em));
        var schedule = new[] { S(7, 30, 16, 0) };
        var work = new AttendanceExpectedWorkDto(employee, date, "Asia/Bangkok", false, "Ready", null,
            new(employment, date.AddDays(-1), null, null, null, null, null, null), assignment, calendar, "TEST", "Test", null, "Weekly", schedule);
        DateTime T(int h, int m, long ticks = 0) => date.ToDateTime(new(h, m), DateTimeKind.Utc).AddHours(-7).AddTicks(ticks);
        AttendanceEventDto E(string d, DateTime t) => new(Guid.NewGuid(), employee, t, date, "Asia/Bangkok", d, "ManualAuthorized", null, null, Guid.NewGuid(), null, t, "test", null, true, "Ready");
        AttendanceEventDto[] Pair(int h, int m, int eh, int em, long ticks = 0) => [E("In", T(h,m,ticks)), E("Out", T(eh,em))];
        AttendanceLeaveEvidence L(int h, int m, int eh, int em, bool paid = true, AttendanceExpectedWorkDto? w = null)
        {
            w ??= work;
            return new(Guid.NewGuid(), "Approved", 1, paid, new(date, employment, assignment, calendar, "TEST", "Test", "Weekly", null,
                new(Guid.NewGuid(), "v1", false, null, true, "NotRequired", null, null, false, false, false),
                w.Intervals.Select(x => new ScheduledLeaveInterval(x.IntervalId,x.StartTime,x.EndTime)).ToArray(), [new(new(h,m),new(eh,em))], (eh*60+em)-(h*60+m)));
        }
        AttendanceDayDto C(AttendanceEventDto[] es, AttendanceLeaveEvidence[]? ls = null, AttendanceExpectedWorkDto? w = null)
            => AttendanceDayCalculator.Calculate(w ?? work, es, ls ?? [], [], T(18,0));
        void Invariant(AttendanceDayDto r, string name)
        {
            check(r.CoveragePartitionAvailable, name+" partition available");
            check(r.ScheduledMilliseconds == r.PresenceCoveredScheduledMilliseconds + r.ApprovedLeaveCoveredScheduledMilliseconds + r.UnexplainedScheduledMilliseconds + r.CoverageTruncationResidualMilliseconds, name+" millisecond residual identity");
            check(AttendanceDayCalculator.Ticks(r.ScheduledIntervals) == AttendanceDayCalculator.Ticks(r.PresenceCoveredScheduledIntervals)+AttendanceDayCalculator.Ticks(r.ApprovedLeaveCoveredIntervals)+AttendanceDayCalculator.Ticks(r.UnexplainedScheduledIntervals), name+" exact tick identity");
        }
        var normal = C(Pair(7,30,16,0)); Invariant(normal,"normal");
        check(normal.Readiness=="Ready" && normal.CoverageTruncationResidualMilliseconds==0 && normal.IsLateUnderCurrentPolicy==false,"D9C zero residual normal");
        var early = C(Pair(7,15,16,20)); Invariant(early,"early/late");
        check(early.ObservedPresenceMilliseconds==545*60000L && early.PresenceCoveredScheduledMilliseconds==510*60000L,"D9C outside schedule not overtime coverage");
        var sub = C(Pair(7,30,16,0,1)); Invariant(sub,"100 ns");
        check(sub.CoverageTruncationResidualMilliseconds==1 && sub.UnexplainedScheduledMilliseconds==0 && sub.UnexplainedScheduledIntervals.Count==1 && sub.RawStartVarianceTicks==1 && sub.Readiness=="Ready","D9C residual is not unexplained or review");
        var mixed = C(Pair(7,30,15,30,1),[L(15,30,16,0)]); Invariant(mixed,"presence/Leave/unexplained");
        check(mixed.PaidLeaveCoveredMilliseconds==1800000 && mixed.CoverageTruncationResidualMilliseconds==1 && mixed.UnexplainedScheduledMilliseconds==0,"D9C minute Leave unchanged by residual");
        foreach (var offset in new long[] { -1,0,1 })
        {
            var grace=C(Pair(7,35,16,0,offset));
            check(grace.IsLateUnderCurrentPolicy==(offset>0) && grace.RawStartVarianceTicks==5*TimeSpan.TicksPerMinute+offset,"D9C exact grace tick "+offset);
        }
        var split=work with { Intervals=[S(7,30,12,0),S(13,0,16,0)] };
        var lunch=C(Pair(7,25,16,5),w:split); Invariant(lunch,"split lunch");
        check(lunch.PresenceCoveredScheduledMilliseconds==450*60000L && lunch.ObservedPresenceMilliseconds==520*60000L,"D9C unscheduled lunch excluded");
        var multi=C([E("Out",T(16,0,-1)),E("In",T(13,0,1)),E("Out",T(12,0,-1)),E("In",T(7,30,1))],w:split); Invariant(multi,"multiple residue");
        check(multi.CoverageTruncationResidualMilliseconds==1 && multi.UnexplainedScheduledMilliseconds==0 && multi.ObservedPresenceIntervals.Count==2,"D9C multiple partition remainders aggregate before truncation");
        var gaps=C([E("In",T(7,30)),E("Out",T(10,0)),E("In",T(11,0)),E("Out",T(15,30))]); Invariant(gaps,"gaps");
        check(gaps.UnexplainedScheduledIntervals.Count==2 && gaps.UnexplainedScheduledMilliseconds==90*60000L,"D9C midday and tail gaps explicit");
        foreach (bool paid in new[]{true,false})
        {
            var full=C([],[L(7,30,16,0,paid)]);Invariant(full,"full Leave "+paid);
            check(full.UnexplainedScheduledMilliseconds==0 && !full.PotentialAbsence && full.Readiness=="Ready" && (paid?full.PaidLeaveCoveredMilliseconds:full.UnpaidLeaveCoveredMilliseconds)==510*60000L,"D9C full Leave not absence "+paid);
        }
        var morning=C(Pair(8,55,16,0),[L(7,30,9,0)]);
        check(morning.ExpectedArrivalUtc==T(9,0) && morning.IsLateUnderCurrentPolicy==false && morning.Findings.Any(x=>x.Code=="PresenceLeaveOverlap"),"D9C morning early presence not late; overlap still requires review without coverage precedence");
        var after=C(Pair(9,0,16,0),[L(7,30,9,0)]);Invariant(after,"morning Leave");
        check(after.ExpectedArrivalUtc==T(9,0) && after.RawStartVarianceTicks==0 && after.IsLateUnderCurrentPolicy==false,"D9C morning Leave shifts arrival boundary");
        var disjoint=C(Pair(9,0,15,30),[L(7,30,9,0),L(15,30,16,0,false)]);Invariant(disjoint,"paid/unpaid disjoint");
        check(disjoint.PaidLeaveCoveredMilliseconds==90*60000L && disjoint.UnpaidLeaveCoveredMilliseconds==30*60000L,"D9C independent paid/unpaid facts");
        var overlap=C(Pair(7,30,16,0),[L(14,0,16,0)]);
        check(overlap.Readiness=="RequiresReview" && overlap.PresenceLeaveOverlapIntervals.Count==1 && overlap.PresenceCoveredScheduledMilliseconds is null && overlap.CoverageTruncationResidualMilliseconds is null,"D9C overlap no authoritative double totals");
        var mismatch=C([], [L(7,30,16,0)],work with { Intervals=[S(8,0,16,0)] });
        check(mismatch.Findings.Any(x=>x.Code=="LeaveScheduleMismatch") && !mismatch.CoveragePartitionAvailable && mismatch.ApprovedLeaves.Count==1,"D9C mismatch both sources retained");
        string[][] malformed=[["In"],["Out"],["In","In","Out"],["Out","Out"],["Unknown"],["In","Unknown","Out"]];
        foreach(var ds in malformed)
        {
            var r=C(ds.Select((d,i)=>E(d,T(8+i,0))).ToArray());
            check(r.Readiness=="RequiresReview" && !r.CoveragePartitionAvailable && r.ObservedPresenceIntervals.Count==0,"D9C ambiguous "+string.Join('/',ds));
        }
        var ties=C([E("In",T(8,0)),E("Out",T(8,0))]);
        check(ties.Findings.Any(x=>x.Code=="ExactTimestampConflict") && ties.ObservedPresenceIntervals.Count==0,"D9C same instant IDs not time ordering");
        var none=C([]);Invariant(none,"no events");check(none.PotentialAbsence && none.Readiness=="RequiresReview","D9C potential absence not finalized");
        foreach(var kind in new[]{"WeeklyNonWorking","PublicHoliday","SchoolHoliday","RestDay"})
        {
            var r=C([],w:work with {Intervals=[],ScheduleKind=kind});Invariant(r,kind);
            check(r.Readiness=="Ready" && !r.PotentialAbsence && r.UnexplainedScheduledMilliseconds==0,"D9C zero schedule "+kind);
        }
        var holiday=C(Pair(8,0,9,0),w:work with {Intervals=[],ScheduleKind="RestDay"});
        check(holiday.ObservedPresenceMilliseconds==3600000 && holiday.PresenceCoveredScheduledMilliseconds==0 && !holiday.PotentialAbsence,"D9C holiday presence no pay inference");
        var exceptional=C(Pair(10,0,11,30),w:work with {Intervals=[S(10,0,11,30)],ScheduleKind="ExceptionalWorkingDay"});Invariant(exceptional,"exceptional");
        check(exceptional.ScheduledMilliseconds==90*60000L,"D9C exceptional replacement schedule");
        foreach(var readiness in new[]{"NotEmployed","WorkCalendarNotConfigured","ConfigurationConflict"})
        { var r=C([],w:work with {Readiness=readiness,Intervals=[]});check(r.Readiness==readiness && r.ScheduledMilliseconds is null && !r.PotentialAbsence,"D9C missing context "+readiness); }
        var untouched=System.Text.Json.JsonSerializer.Serialize(new{work,leaves=disjoint.ApprovedLeaves,events=normal.Events});
        C(normal.Events.ToArray(),disjoint.ApprovedLeaves.ToArray());
        check(untouched==System.Text.Json.JsonSerializer.Serialize(new{work,leaves=disjoint.ApprovedLeaves,events=normal.Events}),"D9C pure calculation never mutates source/Leave/Payroll state");
    }
}
