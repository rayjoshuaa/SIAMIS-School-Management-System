using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Services;

internal static class Section33WageRegressionTests
{
    public static void Run(Action<bool, string> check)
    {
        var resolver = new Section33ContributionWageResolver();
        Section33WageLine Line(string treatment, decimal amount, string type = "Earning", string source = "Manual", Guid? sourceId = null)
            => new(Guid.NewGuid(), "SYNTHETIC", "Synthetic earning", type, amount, treatment, source, sourceId);
        Section33ContributionWageResult Resolve(params Section33WageLine[] lines) => resolver.Resolve("Applicable", lines);
        check(Resolve(Line("Included", 100)).ContributionWageCandidate == 100, "D5B Included earning contributes");
        check(Resolve(Line("Excluded", 100)).ContributionWageCandidate == 0, "D5B explicit Excluded resolved zero candidate");
        var unknown = Line("Unknown", 100, source: "Assignment", sourceId: Guid.NewGuid());
        var unresolved = Resolve(Line("Included", 200), unknown);
        check(unresolved.Status == "Unresolved" && unresolved.ContributionWageCandidate is null, "D5B Unknown does not produce partial/zero wage");
        check(unresolved.Issues.Single().PayrollComponentId == unknown.PayrollComponentId
            && unresolved.Issues.Single().SourceId == unknown.SourceId && unresolved.Issues.Single().LineIndex == 1,
            "D5B Unknown identifies component and source");
        check(resolver.Resolve("NotApplicable", [unknown]).Status == "NotApplicable"
            && resolver.Resolve("NotApplicable", [unknown]).ContributionWageCandidate is null,
            "D5B NotApplicable skips irrelevant classification, no amount");
        check(resolver.Resolve("Unknown", []).Status == "Unresolved"
            && resolver.Resolve("Unknown", []).ContributionWageCandidate is null, "D5B enrollment Unknown not silently exempt");
        check(resolver.Resolve("invalid", []).Status == "InvalidInput", "D5B invalid applicability rejected");
        check(Resolve(Line("Included", 900, "Deduction"), Line("Unknown", 100, "Deduction"), Line("Included", 200)).ContributionWageCandidate == 200,
            "D5B deductions never contribute or block classification");
        check(Resolve(Line("Included", 10.1234m), Line("Included", 20.5678m)).ContributionWageCandidate == 30.6912m,
            "D5B multiple Included lines summed without statutory rounding");
        check(Resolve(Line("Invalid", 10)).Status == "InvalidInput" && Resolve(Line("Invalid", 10)).ContributionWageCandidate is null,
            "D5B invalid earning classification not silently excluded");
        check(Resolve(Line("Included", -1)).Status == "InvalidInput", "D5B negative earning rejected");
        check(Resolve(Line("Included", decimal.MaxValue), Line("Included", 1)).Status == "InvalidInput", "D5B decimal overflow safe failure");

        var calculator = new PayrollCalculationService();
        var basic = new PayrollComponent { Code = "BASIC", Name = "Basic Salary", Category = "Earning", SsoWageTreatment = "Unknown" };
        var component = new PayrollComponent { Code = "EARN", Name = "Synthetic earning", Category = "Earning", SsoWageTreatment = "Included" };
        var assignment = new EmployeePayrollComponentAssignment { PayrollComponentId = component.Id, PayrollComponent = component, Amount = 1000 };
        ApplicablePayrollRuleDto Rule(string mode) => new(Guid.NewGuid(), "RULE", "Synthetic rule", component.Id, component.Code, component.Name,
            "Earning", mode, "Other", "Earning", 1, "FixedAmount", null, null, 300, null, null, "Employee", new(2026, 1, 1), null,
            "Synthetic", [], PayrollComponentSsoWageTreatment: component.SsoWageTreatment);
        PayrollCalculationResult Calculate(string mode) => calculator.Calculate(new(Guid.NewGuid(), "SYNTHETIC", "Synthetic"), 30000, [assignment], basic, [Rule(mode)]);
        var before = Calculate("Supplement");
        check(resolver.Resolve("Applicable", before.Lines.Select(Section33WageLine.FromCalculatedLine).ToArray()).Status == "Unresolved",
            "D5B BasicSalary Unknown follows configured source, fails statutory candidate");
        // Synthetic in-memory classification only; never classifies an existing database component.
        basic.SsoWageTreatment = "Excluded";
        var supplement = Calculate("Supplement");
        check(resolver.Resolve("Applicable", supplement.Lines.Select(Section33WageLine.FromCalculatedLine).ToArray()).ContributionWageCandidate == 1300,
            "D5B final Supplement assignment and rule counted once each, same component");
        var replacement = Calculate("ReplaceAssignment");
        check(resolver.Resolve("Applicable", replacement.Lines.Select(Section33WageLine.FromCalculatedLine).ToArray()).ContributionWageCandidate == 300
            && replacement.Lines.All(line => line.SourceId != assignment.EmployeePayrollComponentAssignmentId),
            "D5B suppressed Included assignment absent from candidate");
        basic.SsoWageTreatment = "Included";
        var includedSalary = Calculate("Supplement");
        check(resolver.Resolve("Applicable", includedSalary.Lines.Select(Section33WageLine.FromCalculatedLine).ToArray()).ContributionWageCandidate == 31300,
            "D5B explicit configurable BasicSalary Included enters candidate");
        basic.SsoWageTreatment = component.SsoWageTreatment = "Excluded";
        check(resolver.Resolve("Applicable", includedSalary.Lines.Select(Section33WageLine.FromCalculatedLine).ToArray()).ContributionWageCandidate == 31300,
            "D5B live classification edit cannot rewrite old calculated snapshot");
        var regenerated = Calculate("Supplement");
        check(resolver.Resolve("Applicable", regenerated.Lines.Select(Section33WageLine.FromCalculatedLine).ToArray()).ContributionWageCandidate == 0,
            "D5B recalculation uses current classification");
        check(before.BasicSalary == regenerated.BasicSalary && before.GrossPay == regenerated.GrossPay
            && before.TaxableEarnings == regenerated.TaxableEarnings && before.TotalDeductions == regenerated.TotalDeductions
            && before.NetPay == regenerated.NetPay, "D5B wage contract does not change payroll totals");
        var storedManual = new EmployeePayrollLineDto(Guid.NewGuid(), Guid.NewGuid(), component.Id, "MAN", "Historical manual earning", "Earning",
            123.4567m, null, null, "Synthetic manual adjustment", "Manual", null, null, null, null, null, null, null, null, null, null,
            false, false, null, null, "Included");
        var manual = Section33WageLine.FromStoredLine(storedManual);
        check(manual.SourceType == "Manual" && manual.SourceId is null && manual.SsoWageTreatmentSnapshot == "Included"
            && Resolve(manual).ContributionWageCandidate == 123.4567m, "D5B stored Manual amount and classification are truthful inputs");
        check(Resolve(manual with { SsoWageTreatmentSnapshot = "Unknown" }).Status == "Unresolved", "D5B Manual Unknown remains unresolved");
        check(typeof(Section33ContributionWageResult).GetProperty("EmployeeAmount") is null
            && typeof(Section33ContributionWageResult).GetProperty("EmployerAmount") is null
            && typeof(Section33ContributionWageResult).GetProperty("ContributionBase") is null,
            "D5B candidate contract exposes no contribution/base amounts");
        check(typeof(Section33WageLine).GetProperty("Nationality") is null, "D5B nationality cannot influence candidate resolution");
    }
}
