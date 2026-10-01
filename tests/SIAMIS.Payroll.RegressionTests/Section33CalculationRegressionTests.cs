using System.Reflection;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Services;

internal static class Section33CalculationRegressionTests
{
    public static void Run(Action<bool, string> check)
    {
        var start = new DateOnly(2026, 10, 1); var end = new DateOnly(2026, 10, 31);
        var sid = Guid.NewGuid(); var pid = Guid.NewGuid(); var eid = Guid.NewGuid();
        var scheme = new StatutorySchemeDto(sid, "TH-SSO-33", "Synthetic", "TH", "SocialSecurity", true, DateTime.UtcNow, DateTime.UtcNow);
        var enrollment = new StatutoryEnrollmentResolution("Applicable", "Synthetic", new(eid, Guid.NewGuid(), sid, start, null, "Applicable", DateTime.UtcNow, DateTime.UtcNow));
        var policy = new StatutoryPolicyDto(pid, sid, "SocialSecurity", "Synthetic", start, null, "THB", "Published", "Synthetic verification only", "SSO-TH-V1", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow,
            new(1.25m, 1.25m, 100m, 200m, "33"), null);
        var calc = new Section33Calculator(new Section33ContributionWageResolver());
        Section33WageLine Line(decimal amount, string treatment = "Included", string type = "Earning")
            => new(Guid.NewGuid(), "TEST", "Synthetic", type, amount, treatment, "Assignment", Guid.NewGuid());
        Section33CalculationOutcome Run(decimal wage) => calc.Calculate(start, end, "THB", scheme, enrollment, policy, [Line(wage)]);
        foreach (var (wage, basis) in new[] { (50m,100m), (100m,100m), (150m,150m), (200m,200m), (250m,200m), (0m,0m) })
        {
            var r = Run(wage);
            check(r.Status == "Calculated" && r.Snapshot!.ContributionWage == wage && r.Snapshot.ContributionBase == basis, $"D5C monthly base selection wage {wage}");
            check(r.Snapshot!.EmployerAmount == r.Snapshot.EmployeeAmount, $"D5C employer equality wage {wage}");
        }
        check(Run(0).Snapshot!.ZeroWage && Run(0).Snapshot!.EmployeeAmount == 0, "D5C zero wage explicit snapshot");
        foreach (var (raw, rounded) in new[] {(1.49m,1m), (1.50m,2m), (1.51m,2m), (0.49m,0m), (0.50m,1m)})
            check(Section33Calculator.RoundWholeBaht(raw) == rounded, $"D5C whole baht half-up {raw}");
        var rMixed = calc.Calculate(start,end,"THB",scheme,enrollment,policy,[Line(120),Line(900,"Excluded"),Line(999,"Unknown","Deduction")]);
        check(rMixed.Snapshot!.ContributionWage == 120 && rMixed.Snapshot.RawEmployeeAmount == 1.5m && rMixed.Snapshot.EmployeeAmount == 2, "D5C only Included earnings plus raw/rounded separation");
        check(calc.Calculate(start,end,"THB",scheme,enrollment,policy,[Line(100,"Unknown")]).Status == "Failed", "D5C Applicable Unknown earning fails");
        check(calc.Calculate(start,end,"THB",scheme,enrollment with { Applicability="NotApplicable" },null,[Line(100,"Unknown")]).Status == "NotApplicable", "D5C explicit NotApplicable skips policy and Unknown wage");
        check(calc.Calculate(start,end,"THB",scheme,new("Unknown","Absent",null),policy,[Line(100)]).Status == "Failed", "D5C missing enrollment fails");
        foreach (var invalid in new StatutoryPolicyDto?[] {null, policy with {Status="Draft"}, policy with {CalculationMethodVersion="SSO-TH-V2"}, policy with {Currency="USD"}, policy with {EffectiveFrom=end.AddDays(1)}, policy with {SocialSecurity=new(1m,2m,100m,200m,"33")} })
            check(calc.Calculate(start,end,"THB",scheme,enrollment,invalid,[Line(100)]).Status == "Failed", "D5C invalid/missing/incompatible policy fails defensively");
        check(calc.Calculate(start,end,"USD",scheme,enrollment,policy,[Line(100)]).Status == "Failed", "D5C no currency conversion");
        check(calc.Calculate(start.AddDays(1),end,"THB",scheme,enrollment,policy,[Line(100)]).Status == "Failed", "D5C incomplete payroll month rejected");
        var snap=Run(150).Snapshot!;
        check(snap.GoverningDate==end && snap.ContributionMonth=="2026-10" && snap.EmployeeRate==1.25m && snap.EmployerRate==1.25m && snap.WageInputs.Count==1 && snap.OfficialReference==policy.OfficialReference, "D5C complete policy/enrollment/month/rates/input historical snapshot");
        var publish = typeof(StatutoryPolicyService).GetMethod("PublicationError", BindingFlags.Static|BindingFlags.NonPublic)!;
        var entity=new StatutoryPolicyVersion {SchemeType="SocialSecurity",OfficialReference="Synthetic",CalculationMethodVersion="SSO-TH-V1",SocialSecurity=new(){EmployeeContributionRate=1,EmployerContributionRate=1,MinimumContributionBase=100,MaximumContributionBase=200,InsuredPersonClassification="33"}};
        check(publish.Invoke(null,[entity]) is null,"D5C equal V1 rates publication valid");
        entity.SocialSecurity.EmployerContributionRate=2;
        check(((string)publish.Invoke(null,[entity])!).Contains("equal"),"D5C unequal V1 rates publication invalid");
        entity.CalculationMethodVersion="SSO-TH-V2";
        check(((string)publish.Invoke(null,[entity])!).Contains("supported D4A"),"D5C future method fails existing method validation rather than generalized equality");
    }
}
