using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Services;
using System.Text.Json;

internal static class D9DReviewTests
{
    public static void Run(Action<bool,string> check, IModel model)
    {
        var employee=Guid.NewGuid(); var date=new DateOnly(2030,1,7);
        var work=new AttendanceExpectedWorkDto(employee,date,"Asia/Bangkok",false,"Ready",null,
            new(Guid.NewGuid(),date.AddDays(-1),null,null,null,null,null,null),Guid.NewGuid(),Guid.NewGuid(),"TEST","Test",null,"Weekly",
            [new(Guid.NewGuid(),new(7,30),new(16,0))]);
        DateTime T(int h,int m,long ticks=0)=>date.ToDateTime(new(h,m),DateTimeKind.Utc).AddHours(-7).AddTicks(ticks);
        AttendanceEventDto E(string direction,DateTime t)=>new(Guid.NewGuid(),employee,t,date,"Asia/Bangkok",direction,"Device","test","test",null,null,t,null,null,false,"Ready");
        var events=new[]{E("In",T(7,30)),E("Out",T(16,0))};
        AttendanceDayDto C(AttendanceEventDto[] es,DateTime? observed=null)=>AttendanceDayCalculator.Calculate(work,es,[],[],observed??T(18,0));
        var clean=C(events); var sources=AttendanceReviewSources.Sources(clean,[]); var hash=AttendanceReviewSources.Fingerprint(sources);
        check(hash.Length==64,"D9D SHA256 source token");
        check(hash==AttendanceReviewSources.Fingerprint(AttendanceReviewSources.Sources(C(events.Reverse().ToArray(),T(19,0)),[])),"D9D source order and read time excluded");
        var renamed=AttendanceDayCalculator.Calculate(work with { CalendarName="Renamed",CalendarCode="Renamed",EmployeeIsActive=true },events,[],[],T(19,0));
        check(hash==AttendanceReviewSources.Fingerprint(AttendanceReviewSources.Sources(renamed,[])),"D9D descriptive names and administrative status excluded");
        var received=C(events.Select(e=>e with{ReceivedAtUtc=T(20,0),Reason="Receipt metadata",EmployeeWasInactive=true}).ToArray());
        check(hash==AttendanceReviewSources.Fingerprint(AttendanceReviewSources.Sources(received,[])),"D9D non-calculation receipt metadata excluded");
        check(AttendanceReviewSources.CanFinalize(clean,false),"D9D clean day directly finalizable");
        var absent=C([]);check(!AttendanceReviewSources.CanFinalize(absent,false)&&AttendanceReviewSources.CanFinalize(absent,true),"D9D absence requires explicit confirmation");
        var ah=AttendanceReviewSources.Fingerprint(AttendanceReviewSources.Sources(absent,[]));
        var confirmation=new AttendanceReviewAction{Action="AbsenceConfirmed",Sequence=1,SourceFingerprint=ah};
        check(AttendanceReviewSources.Confirmed(absent,ah,[confirmation]),"D9D confirmed source-bound absence");
        check(!AttendanceReviewSources.Confirmed(absent,hash,[confirmation]),"D9D source change invalidates confirmation");
        check(!AttendanceReviewSources.Confirmed(absent,ah,[confirmation,new(){Action="Reopened",Sequence=2}]),"D9D reopen requires renewed absence review");
        var duplicate=E("In",T(7,31));var raw=C([events[0],duplicate,events[1]]);
        check(!AttendanceReviewSources.CanFinalize(raw,false),"D9D ambiguous evidence blocks finalization");
        var excluded=new AttendanceReviewAction{Action="Excluded",AttendanceEventId=duplicate.AttendanceEventId,Sequence=1};
        var resolved=AttendanceReviewSources.Calculate(raw,[excluded]);
        check(resolved.Events.Count==2&&raw.Events.Count==3&&AttendanceReviewSources.CanFinalize(resolved,false),"D9D exclusion recalculates without deleting raw evidence");
        var included=new AttendanceReviewAction{Action="Included",AttendanceEventId=duplicate.AttendanceEventId,Sequence=2};
        check(!AttendanceReviewSources.CanFinalize(AttendanceReviewSources.Calculate(raw,[excluded,included]),false),"D9D newest adjudication effective");
        check(AttendanceReviewSources.Changes(sources,sources with{Evidence="changed"},new Dictionary<Guid,string>()).Single().Code=="AttendanceEvidenceChanged","D9D structured evidence staleness");
        check(AttendanceReviewSources.Changes(sources,sources with{Work="changed"},new Dictionary<Guid,string>()).Single().Code=="ExpectedWorkChanged","D9D structured schedule staleness");
        check(AttendanceReviewSources.Changes(sources,sources with{Policy="changed"},new Dictionary<Guid,string>()).Single().Code=="AttendancePolicyChanged","D9D structured policy staleness");
        var leave=Guid.NewGuid();var old=sources with{Leave="old",LeaveIds=[leave]};
        check(AttendanceReviewSources.Changes(old,sources,new Dictionary<Guid,string>{{leave,"Cancelled"}}).Any(x=>x.Code=="ApprovedLeaveCancelled"&&x.SourceIds.Contains(leave)),"D9D cancellation explanation");
        foreach(var ticks in new long[]{0,1})
        {
            var r=C([E("In",T(7,35,ticks)),events[1]]);
            check(r.IsLateUnderCurrentPolicy==(ticks==1)&&AttendanceReviewSources.CanFinalize(r,false),"D9D grace ticks frozen "+ticks);
            var frozen=AttendanceReviewSources.Parse<AttendanceFinalizedSnapshot>(AttendanceReviewSources.Serialize(new AttendanceFinalizedSnapshot(1,r,r.Events,[],false)));
            check(frozen.Calculation.RawStartVarianceTicks==r.RawStartVarianceTicks&&frozen.Calculation.CoverageTruncationResidualMilliseconds==r.CoverageTruncationResidualMilliseconds,"D9D snapshot exact precision "+ticks);
        }
        foreach(var type in new[]{typeof(AttendanceReviewCase),typeof(AttendanceReviewAction),typeof(FinalizedAttendanceRevision)})
        {
            var e=model.FindEntityType(type)!;
            check(e.GetForeignKeys().All(x=>x.DeleteBehavior==DeleteBehavior.NoAction),"D9D no cascades "+type.Name);
            check(e.FindProperty("ActorUserId")!.IsNullable,"D9D no fabricated actor "+type.Name);
        }
        foreach(var type in new[]{typeof(AttendanceReviewAction),typeof(FinalizedAttendanceRevision)})
        foreach(var state in new[]{EntityState.Modified,EntityState.Deleted})
        {
            using var db=new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
            db.Entry(Activator.CreateInstance(type)!).State=state;
            bool rejected=false;try{db.SaveChanges();}catch(InvalidOperationException){rejected=true;}
            check(rejected,"D9D immutable EF guard "+type.Name+state);
        }
        foreach(var field in new[]{"actorUserId","scheduledMilliseconds","sourceFingerprint"})
        {
            bool rejected=false;try{JsonSerializer.Deserialize<AttendanceReviewRequest>("{\""+field+"\":null}",AttendanceReviewSources.Json);}catch(JsonException){rejected=true;}
            check(rejected,"D9D no client-forged "+field);
        }
        foreach(bool deleted in new[]{false,true})
        {
            using var db=new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options);
            var c=new AttendanceReviewCase();db.Attach(c);
            if(deleted)db.Remove(c);else c.OriginalCalculationJson="{}";
            bool rejected=false;try{db.SaveChanges();}catch(InvalidOperationException){rejected=true;}
            check(rejected,"D9D case original snapshot immutable "+deleted);
        }
        using(var db=new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=True").Options))
        {
            var c=new AttendanceReviewCase();db.Attach(c);c.State="Resolved";
            typeof(SIAMISDbContext).GetMethod("UpdateTimestamps",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(db,[]);
            check(db.Entry(c).Property(x=>x.State).IsModified,"D9D case lifecycle state may change without overwriting original evidence");
        }
    }
}
