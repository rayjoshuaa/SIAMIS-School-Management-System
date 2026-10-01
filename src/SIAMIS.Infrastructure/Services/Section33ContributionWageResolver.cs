using SIAMIS.Application.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Pure classification resolver shared by the Section 33 calculator; it does not select bases or calculate contributions.</summary>
public sealed class Section33ContributionWageResolver : ISection33ContributionWageResolver
{
    public Section33ContributionWageResult Resolve(string applicability, IReadOnlyList<Section33WageLine> finalLines)
    {
        if (applicability == "NotApplicable")
            return new("NotApplicable", null, "Explicitly NotApplicable; SSO wage classification is not required. No contribution was calculated.", []);
        if (applicability == "Unknown")
            return new("Unresolved", null, "Enrollment applicability is Unknown; it is not an exemption or a zero contribution.", []);
        if (applicability != "Applicable")
            return new("InvalidInput", null, "Applicability must be Applicable, NotApplicable, or Unknown.", []);

        var issues = new List<Section33WageIssue>();
        var includedAmounts = new List<decimal>();
        for (var index = 0; index < finalLines.Count; index++)
        {
            var line = finalLines[index];
            // Deduction classification, source and amount cannot contribute to the wage candidate.
            if (line.ComponentType == "Deduction") continue;
            string? code = null;
            string? message = null;
            if (line.ComponentType != "Earning")
            {
                code = "invalid_component_type";
                message = "Final line must identify Earning or Deduction; no treatment was inferred.";
            }
            else if (line.SsoWageTreatmentSnapshot == "Unknown")
            {
                code = "unknown_treatment";
                message = "Applicable employee has an earning with unresolved SsoWageTreatmentSnapshot.";
            }
            else if (line.SsoWageTreatmentSnapshot is not ("Included" or "Excluded"))
            {
                code = "invalid_treatment";
                message = "Earning snapshot must explicitly identify Unknown, Included, or Excluded.";
            }
            else if (line.Amount < 0)
            {
                code = "invalid_amount";
                message = "Final earning amount cannot be negative.";
            }
            else if (line.SsoWageTreatmentSnapshot == "Included") includedAmounts.Add(line.Amount);
            if (code is not null)
                issues.Add(new(index, line.PayrollComponentId, line.ComponentCode, line.ComponentName,
                    line.SourceType, line.SourceId, code, message!));
        }
        if (issues.Count > 0)
            return new(issues.Any(issue => issue.Code != "unknown_treatment") ? "InvalidInput" : "Unresolved",
                null, "No contribution-wage candidate was resolved. Inspect the identified earning components/sources.", issues);
        try
        {
            // Each final line contributes once. Distinct assignment/rule/manual lines sharing a component remain distinct earnings.
            decimal wage = 0;
            foreach (var amount in includedAmounts) wage = checked(wage + amount);
            return new("Resolved", wage, "Sum of final Included earning amounts only. No statutory base, rounding or employee/employer contribution was calculated.", []);
        }
        catch (OverflowException)
        {
            return new("InvalidInput", null, "Included earning amounts exceed the supported decimal arithmetic range.", []);
        }
    }
}
