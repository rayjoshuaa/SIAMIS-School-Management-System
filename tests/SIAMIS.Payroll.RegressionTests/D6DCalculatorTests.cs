using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Services;

internal static class D6DCalculatorTests
{
    public static void Run(Action<bool, string> check, IModel model)
    {
        var calc = new PitCalculator(new PitIncomeResolver());
        var employee = Guid.NewGuid(); var declarationId = Guid.NewGuid(); var period = Guid.NewGuid();
        var schemeId = Guid.NewGuid(); var policyId = Guid.NewGuid(); var component = Guid.NewGuid();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var scheme = new StatutorySchemeDto(schemeId, "TH-PIT", "Synthetic", "TH", "PersonalIncomeTax", true, now, now);
        var config = new PitPolicyDto(2026, 0, 1, 0, "Synthetic regular monthly",
            [new(1, 0, null, 1)], 0, 0, 0, 0, 3, 4);
        var policy = new StatutoryPolicyDto(policyId, schemeId, "PersonalIncomeTax", "Synthetic",
            new(2026, 1, 1), new(2026, 12, 31), "THB", "Published", "Synthetic test only", "PIT-TH-V1", now, now, now, null, config);
        var opening = new EmployeeTaxOpeningBalanceDto("ConfirmedZero", "THB", 0, 0, 0,
            new(2025, 12, 31), "Synthetic", now, now, now, "CurrentEmployer", true, "PIT-TH-V1");
        var declaration = new EmployeeTaxDeclarationDto(new(declarationId, employee, 2026, 1, null, "Verified", true, now, null, now, now),
            "Synthetic", [], opening, new(declarationId, employee, 2026, 1, "Resident", "StandardSection40_1", "Verified", now, null), 0);
        PitIncomeLine Line(decimal amount, string income = "Included", string payment = "Regular", string type = "Earning")
            => new(component, "TEST", "Synthetic", type, amount, income, "BasicSalary", null, payment);
        PitCalculationInput Input(decimal amount, int n = 12, int ordinal = 1) => new(employee, period, null,
            new(2026, 6, 30), scheme, policy, declaration, new(n, ordinal, "Synthetic reviewed monthly schedule"),
            [Line(amount)], [], [], [new(PitSsoSourceKind.OpeningBalance, 0, opening.AsOfDate, null, null, declarationId)], true, "NotApplicable");
        bool Review(PitCalculationInput x) => calc.Calculate(x).Status == "RequiresReview";
        PitCalculationSnapshot Result(PitCalculationInput x) { var r=calc.Calculate(x); check(r.Status=="Calculated", "D6D resolved vector"); return r.Calculation!; }
        foreach (var raw in new[] {12.065m, 12.064m, 12.069m, 12.06m, 0m})
        {
            var x = Input(raw * 100, 1, 1);
            var r = Result(x);
            var allocated = decimal.Truncate(raw * 100) / 100;
            check(r.RawAnnualTax == raw && r.AllocatableAnnualWithholding == allocated
                && r.SubSatangRemainder == raw - allocated && r.CurrentWithholding == allocated, "D6D raw/allocatable/sub-satang " + raw);
            check(calc.Calculate(x).CalculationSnapshotJson == calc.Calculate(x).CalculationSnapshotJson, "D6D byte-identical repeat " + raw);
        }
        var thirds = Input(1000, 3) with { Policy = policy with { PersonalIncomeTax = config with { Brackets = [new(1, 0, null, 10)] , PersonalAllowanceAmount = 2000 } } };
        check(Result(thirds).RegularWithholdingAmount == 33.33m, "D6D N=3 regular allocation");
        var finalOpening = opening with { State="VerifiedAmount", AsOfDate=new(2026, 5, 31), PriorTaxWithheld=66.66m };
        var finalInput = thirds with { Schedule=new(3,3,"Synthetic proven final payment"), Declaration=declaration with {OpeningBalance=finalOpening},
            SsoSources=[new(PitSsoSourceKind.OpeningBalance,0,finalOpening.AsOfDate,null,null,declarationId)] };
        var final = Result(finalInput);
        check(final.CurrentWithholding == 33.34m && final.FinalAllocationResidual == .01m
            && final.PriorRecognizedWithholding + final.CurrentWithholding == 100, "D6D final residual reconciles");
        var over = finalInput with { Declaration = finalInput.Declaration with { OpeningBalance=finalOpening with {PriorTaxWithheld=101} } };
        check(Result(over).CurrentWithholding==0 && Result(over).OverWithheldAmount==1, "D6D no negative/refund");
        check(Result(Input(100)).ApplicablePaymentCount==12, "D6D full-year N=12");
        foreach (var s in new PitPaymentSchedule[] {new(null,null,null),new(12,null,"Unknown ordinal"),new(3,1,null),new(3,1,"partial",false),new(3,1,"leaver",true,true),new(3,1,"year end",true,false,true)})
            check(Review(Input(100) with {Schedule=s}), "D6D schedule/partial/final branch fails closed");
        foreach (var treatment in new[] {"Unknown", "Special"})
            check(Review(Input(100) with {CurrentLines=[Line(100,"Included",treatment)]}), "D6D Included payment " + treatment);
        check(Result(Input(100) with {CurrentLines=[Line(100,"Excluded","Special"),Line(500,"Unknown","Unknown","Deduction")]}).CurrentRegularIncome==0, "D6D Excluded/deduction not income");
        check(Review(Input(100) with {CurrentLines=[Line(100,"Unknown")]}), "D6D Unknown income unresolved");
        foreach (var status in new[] {"Draft","Calculated","Approved","Cancelled","Paid"})
        {
            var payroll = Guid.NewGuid();
            var x = Input(100) with {History=[new(payroll,employee,new(2026,2,28),status,[Line(50)])],
                PriorWithholding=status=="Paid" ? [new(Guid.NewGuid(),payroll,employee,new(2026,2,28),status,"THB","PIT-TH-V1",1)] : []};
            var r=Result(x); check(r.PriorRecognizedIncome==(status=="Paid"?50:0) && r.PriorRecognizedWithholding==(status=="Paid"?1:0), "D6D historical recognition " + status);
        }
        check(Review(Input(100) with {HistoricalWithholdingAuthoritative=false}), "D6D missing exact PIT ledger unresolved");
        var currentPayroll=Guid.NewGuid(); var ssoResult=Guid.NewGuid();
        var ssoInput=Input(100) with {CurrentPayrollId=currentPayroll,CurrentSsoStatus="Resolved",
            SsoSources=[new(PitSsoSourceKind.OpeningBalance,0,opening.AsOfDate,null,null,declarationId),new(PitSsoSourceKind.CurrentPayroll,25,new(2026,6,30),ssoResult,currentPayroll,null)]};
        check(Result(ssoInput).RecognizedEmployeeSso==25 && Result(ssoInput).NetTaxableIncome==1175, "D6D exact employee SSO no projection");
        check(Review(ssoInput with {SsoSources=[]}), "D6D missing SSO sources unresolved");
        foreach (var residency in new[] {"Resident","NonResident"})
            check(Result(Input(100) with {Declaration=declaration with {Treatment=declaration.Treatment with {ResidencyStatus=residency}}}).CurrentRegularIncome==100, "D6D standard treatment independent nationality " + residency);
        check(Review(Input(100) with {Declaration=declaration with {Treatment=declaration.Treatment with {EmploymentTaxTreatment="RequiresReview"}}}), "D6D treatment gate");
        check(Review(Input(100) with {Declaration=declaration with {Declaration=declaration.Declaration with {Status="Draft"}}}), "D6D Draft rejected");
        check(Review(Input(100) with {Declaration=declaration with {Declaration=declaration.Declaration with {IsCurrentVerified=false}}}), "D6D unselected rejected");
        check(Review(Input(100) with {Declaration=declaration with {OpeningBalance=opening with {State="Unknown"}}}), "D6D Unknown opening");
        foreach (var p in new[] {policy with {Status="Draft"}, policy with {CalculationMethodVersion="wrong"},policy with {Currency="USD"},policy with {PersonalIncomeTax=config with {TaxYear=2025}}})
            check(Review(Input(100) with {Policy=p}), "D6D incompatible policy");
        check(Review(Input(100) with {Scheme=scheme with {Jurisdiction="XX"}}), "D6D wrong jurisdiction");
        var progressive=config with {Brackets=[new(1,0,100,0),new(2,100,200,10),new(3,200,null,20)]};
        foreach (var (income,tax) in new[] {(0m,0m),(100m,0m),(150m,5m),(200m,10m),(250m,20m)})
            check(Result(Input(income,1,1) with {Policy=policy with {PersonalIncomeTax=progressive}}).RawAnnualTax==tax, "D6D progressive boundary " + income);
        check(Review(Input(100) with {Policy=policy with {PersonalIncomeTax=progressive with {Brackets=[new(1,0,100,0),new(2,101,null,10)]}}}), "D6D bracket gap rejected");
        var expense=config with {EmploymentExpenseDeductionRate=10,EmploymentExpenseDeductionCap=50};
        foreach (var (income,pre,allowed) in new[] {(100m,10m,10m),(1000m,100m,50m)})
        {var r=Result(Input(income,1,1) with {Policy=policy with {PersonalIncomeTax=expense}});check(r.Expense.PreCapAmount==pre && r.Expense.AllowedAmount==allowed,"D6D expense cap");}
        EmployeeTaxClaimDto Claim(string kind,int quantity=1,string? relationship=null,bool? additional=null) => new(Guid.NewGuid(),kind,null,quantity,"Synthetic reviewed",null,now,now,relationship,additional);
        var allowances=config with {PersonalAllowanceAmount=10,SpouseAllowanceAmount=20,ChildAllowanceAmount=30,AdditionalChildAllowanceAmount=40,ParentAllowanceAmount=50};
        var claimed=Input(1000,1,1) with {Policy=policy with {PersonalIncomeTax=allowances},Declaration=declaration with {TotalLivingLawfulChildren=1,Claims=[Claim("Spouse"),Claim("Child",1,"Lawful",true),Claim("Child",1,"Adopted",false),Claim("Parent",2)]}};
        var a=Result(claimed).Allowances;
        check(a.Personal==10 && a.Spouse==20 && a.OrdinaryChild==60 && a.AdditionalChild==40 && a.Parent==100, "D6D typed allowances");
        check(Review(claimed with {Declaration=claimed.Declaration with {Claims=[Claim("Child",3,"Adopted",false)]}}), "D6D adopted capacity rejected no trim");
        check(Review(claimed with {Declaration=claimed.Declaration with {Claims=[Claim("Parent",5)]}}), "D6D parent limit rejected");
        check(Result(Input(100)).Allowances.Spouse==0, "D6D absent spouse zero");
        check(Result(Input(100) with {Declaration=declaration with {OpeningBalance=opening with {State="VerifiedAmount",PriorTaxableEmploymentIncome=50,AsOfDate=new(2026,2,28)}}, SsoSources=[new(PitSsoSourceKind.OpeningBalance,0,new(2026,2,28),null,null,declarationId)]}).PriorRecognizedIncome==50, "D6D VerifiedAmount pre-expense opening preserved");
        check(Review(Input(100) with {CurrentLines=[Line(-1)]}), "D6D negative earning invalid");
        check(Review(Input(100) with {Policy=policy with {PersonalIncomeTax=config with {Brackets=[]}}}), "D6D incomplete brackets");
        check(Review(Input(100) with {Policy=policy with {PersonalIncomeTax=config with {Brackets=[new(1,0,10,1),new(2,9,null,2)]}}}), "D6D overlap rejected");
        var hId=Guid.NewGuid(); var hDate=new DateOnly(2026,2,28);
        var historical=Input(100) with {History=[new(hId,employee,hDate,"Paid",[Line(50)])], PriorWithholding=[new(Guid.NewGuid(),hId,employee,hDate,"Paid","THB","PIT-TH-V1",1)]};
        check(Review(historical with {History=[new(hId,employee,hDate,"Paid",[Line(50,"Included","Special")])]}), "D6D historical Special snapshot review");
        check(Review(historical with {PriorWithholding=[]}), "D6D Paid income without PIT result authority");
        check(Review(historical with {PriorWithholding=[historical.PriorWithholding[0] with {Currency="USD"}]}), "D6D incompatible prior result currency");
        check(Review(historical with {PriorWithholding=[historical.PriorWithholding[0] with {MethodVersion="wrong"}]}), "D6D incompatible prior result method");
        check(Review(historical with {History=[historical.History[0],historical.History[0]]}), "D6D no duplicate history");
        check(Review(historical with {History=[historical.History[0] with {PayDate=historical.GoverningDate}]}), "D6D same-day ownership review");
        check(Result(historical with {History=[historical.History[0] with {PayDate=new(2025,2,28)}],PriorWithholding=[]}).PriorRecognizedIncome==0, "D6D exclude other tax year");
        check(Result(historical with {CurrentPayrollId=hId,PriorWithholding=[]}).PriorRecognizedIncome==0, "D6D current payroll not double counted");
        check(Result(Input(100) with {CurrentLines=[Line(50),Line(200,"Excluded","Unknown")]}).CurrentRegularIncome==50, "D6D excluded unknown payment harmless");
        check(Review(Input(100) with {Declaration=declaration with {Claims=[Claim("Insurance")]}}), "D6D unsupported deduction facts review");
        check(Review(ssoInput with {SsoSources=[..ssoInput.SsoSources,ssoInput.SsoSources[1]]}), "D6D duplicate current SSO rejected");
        var entity=model.FindEntityType(typeof(PayrollComponent))!;
        check(entity.GetSeedData().All(z=>(string?)z["PitPaymentTreatment"]=="Unknown"), "D6D all component seeds Unknown");
        foreach(var (type,name) in new[] {(typeof(PayrollComponent),"PitPaymentTreatment"),(typeof(EmployeePayrollLine),"PitPaymentTreatmentSnapshot")})
        {var e=model.FindEntityType(type)!; check((string?)e.FindProperty(name)!.GetDefaultValue()=="Unknown" && e.GetCheckConstraints().Any(z=>z.Sql.Contains("'Regular'")), "D6D schema default/check " + name);}
        check(model.FindEntityType(typeof(EmployeePayrollPitResult)) != null, "D6E dedicated PIT ledger added after D6D");
        check(typeof(PitPriorWithholding).GetProperty("PitResultId")!=null && typeof(PitSsoSource).GetProperty("EmployerAmount")==null, "D6D typed historical/PIT/employee-only SSO authority");
    }
}
