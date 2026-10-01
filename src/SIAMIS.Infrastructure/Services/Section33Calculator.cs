using SIAMIS.Application.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Approved SIAMIS SSO-TH-V1 contract; no legal numeric defaults.</summary>
public sealed class Section33Calculator(ISection33ContributionWageResolver wageResolver) : ISection33Calculator
{
    public Section33CalculationOutcome Calculate(DateOnly start, DateOnly end, string currency,
        StatutorySchemeDto scheme, StatutoryEnrollmentResolution enrollment,
        StatutoryPolicyDto? policy, IReadOnlyList<Section33WageLine> lines)
    {
        if (!BasicSalaryPeriod.IsSupported(start, end)) return Fail(BasicSalaryPeriod.ValidationMessage);
        if (scheme.Code != "TH-SSO-33" || scheme.Jurisdiction != "TH" || scheme.SchemeType != "SocialSecurity")
            return Fail("Section 33 requires the TH-SSO-33 SocialSecurity scheme in jurisdiction TH.");
        if (enrollment.Applicability == "NotApplicable") return new("NotApplicable", "Section 33 enrollment is explicitly NotApplicable; no automatic contribution.");
        var e = enrollment.Enrollment;
        if (enrollment.Applicability != "Applicable" || e is null)
            return Fail("Section 33 enrollment is Unknown or missing on PayrollPeriod.EndDate.");
        if (e.StatutorySchemeId != scheme.StatutorySchemeId || e.Applicability != "Applicable"
            || e.EffectiveFrom > end || e.EffectiveTo < end)
            return Fail("Section 33 enrollment does not cover the governing date or scheme.");
        if (policy is null || policy.Status != "Published" || policy.StatutorySchemeId != scheme.StatutorySchemeId
            || policy.SchemeType != "SocialSecurity" || policy.EffectiveFrom > end || policy.EffectiveTo < end)
            return Fail("Section 33 requires an effective Published policy on PayrollPeriod.EndDate.");
        if (policy.CalculationMethodVersion != "SSO-TH-V1") return Fail("Unsupported Section 33 CalculationMethodVersion; expected SSO-TH-V1.");
        if (currency != "THB" || policy.Currency != "THB") return Fail("Section 33 calculation supports THB only; currency conversion is unsupported.");
        var c = policy.SocialSecurity;
        if (c is null || c.InsuredPersonClassification != "33" || !c.EmployeeContributionRate.HasValue
            || !c.EmployerContributionRate.HasValue || !c.MinimumContributionBase.HasValue || !c.MaximumContributionBase.HasValue)
            return Fail("Section 33 policy requires classification 33 and complete rates/base parameters.");
        if (new[] { c.EmployeeContributionRate.Value, c.EmployerContributionRate.Value,
            c.MinimumContributionBase.Value, c.MaximumContributionBase.Value }.Any(x => x < 0 || x > Maximum || decimal.Round(x, 4) != x)
            || c.MinimumContributionBase > c.MaximumContributionBase)
            return Fail("Section 33 policy parameters are invalid.");
        if (c.EmployeeContributionRate != c.EmployerContributionRate)
            return Fail("SSO-TH-V1 requires equal EmployeeRate and EmployerRate; the Published policy was not changed.");
        if (string.IsNullOrWhiteSpace(policy.OfficialReference)) return Fail("Published Section 33 policy is missing its official reference.");
        var wage = wageResolver.Resolve("Applicable", lines);
        if (wage.Status != "Resolved") return Fail(wage.Message + " " + string.Join(" ", wage.Issues.Select(x => $"{x.ComponentCode} ({x.SourceType}/{x.SourceId}): {x.Code}: {x.Message}")));
        try
        {
            var amount = wage.ContributionWageCandidate!.Value;
            if (amount > Maximum) return Fail("Section 33 contribution wage exceeds decimal(19,4).");
            var basis = amount == 0 ? 0 : Math.Min(Math.Max(amount, c.MinimumContributionBase.Value), c.MaximumContributionBase.Value);
            // D4A stores percentage points, exactly as PayrollRule does.
            var raw = checked(basis * c.EmployeeContributionRate.Value / 100m);
            var employee = RoundWholeBaht(raw);
            if (employee > Maximum) return Fail("Section 33 contribution exceeds decimal(19,4).");
            return new("Calculated", "Section 33 contribution calculated.", new(1, "SSO-TH-V1", scheme.Code, scheme.Jurisdiction,
                scheme.StatutorySchemeId, policy.StatutoryPolicyVersionId, policy.Version,
                policy.EffectiveFrom, policy.EffectiveTo, policy.OfficialReference!, e.EmployeeStatutoryEnrollmentId,
                e.EffectiveFrom, e.EffectiveTo, "Applicable", "33", end.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                end, "THB", amount, basis, c.MinimumContributionBase.Value, c.MaximumContributionBase.Value,
                c.EmployeeContributionRate.Value, c.EmployerContributionRate.Value, raw, employee, employee, amount == 0,
                "ZeroWageOtherwiseMonthlyClamp", "WholeBahtHalfUp", lines.ToArray()));
        }
        catch (OverflowException) { return Fail("Section 33 decimal calculation overflow; no contribution was produced."); }
    }

    public static decimal RoundWholeBaht(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var whole = decimal.Floor(amount);
        return amount - whole >= 0.50m ? checked(whole + 1m) : whole;
    }
    private const decimal Maximum = 999_999_999_999_999.9999m;
    private static Section33CalculationOutcome Fail(string message) => new("Failed", message);
}
