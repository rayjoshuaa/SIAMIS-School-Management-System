using SIAMIS.Application.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Pure final-line classification. It neither calculates PIT nor changes payroll totals.</summary>
public sealed class PitIncomeResolver : IPitIncomeResolver
{
    public PitIncomeResolution Resolve(IReadOnlyList<PitIncomeLine> finalLines)
    {
        var issues = new List<PitIncomeIssue>();
        decimal candidate = 0;
        for (var i = 0; i < finalLines.Count; i++)
        {
            var line = finalLines[i];
            if (line.ComponentType == "Deduction") continue;
            string? code = line.ComponentType != "Earning" ? "invalid_component_type"
                : line.PitIncomeTreatmentSnapshot is not ("Unknown" or "Included" or "Excluded") ? "invalid_treatment"
                : line.Amount < 0 ? "invalid_amount"
                : line.PitIncomeTreatmentSnapshot == "Unknown" ? "unknown_treatment" : null;
            if (code is null && line.PitIncomeTreatmentSnapshot == "Included")
            {
                try { candidate = checked(candidate + line.Amount); }
                catch (OverflowException) { code = "amount_overflow"; }
            }
            if (code is not null)
                issues.Add(new(i, line.PayrollComponentId, line.ComponentCode, line.ComponentName,
                    line.SourceType, line.SourceId, code, "PIT income cannot be resolved from this final earning classification/input."));
        }
        if (issues.Count > 0)
            return new(issues.Any(x => x.Code != "unknown_treatment") ? "InvalidInput" : "Unresolved", null,
                "No complete candidate is available. Review the identified earning components and sources.", issues);
        return new("Resolved", candidate, "Sum of final Included earnings only; no PIT or deductions calculated.", []);
    }
}
