using System.Text.Json;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Pure PIT-TH-V1 regular monthly engine. No EF, clock, mutable masters, floats or payroll writes.</summary>
public sealed class PitCalculator(IPitIncomeResolver incomeResolver) : IPitCalculator
{
    public PitCalculationResult Calculate(PitCalculationInput input)
    {
        try { return CalculateCore(input); }
        catch (OverflowException) { return Review("Decimal monetary/count overflow; inputs require review."); }
    }

    private PitCalculationResult CalculateCore(PitCalculationInput x)
    {
        var p = x.Policy; var c = p.PersonalIncomeTax; var d = x.Declaration;
        var o = d.OpeningBalance; var date = x.GoverningDate;
        if (x.EmployeeId == Guid.Empty || x.PayrollPeriodId == Guid.Empty
            || p.StatutoryPolicyVersionId == Guid.Empty || p.StatutorySchemeId != x.Scheme.StatutorySchemeId
            || x.Scheme.Code != "TH-PIT" || x.Scheme.Jurisdiction != "TH"
            || x.Scheme.SchemeType != "PersonalIncomeTax" || !x.Scheme.IsActive
            || p.SchemeType != "PersonalIncomeTax" || p.Status != "Published" || !p.PublishedAt.HasValue
            || p.CalculationMethodVersion != "PIT-TH-V1" || p.Currency != "THB"
            || date < p.EffectiveFrom || (p.EffectiveTo.HasValue && date > p.EffectiveTo)
            || c is null || c.TaxYear != date.Year || c.RateUnit != "PercentagePoints")
            return Review("A compatible Published TH-PIT / TH / THB / PIT-TH-V1 policy for PayDate and TaxYear is required.");
        var treatment = EmployeeTaxTreatmentResolver.Resolve(d.Treatment);
        if (treatment.Status != "Approved" || d.Declaration.EmployeeId != x.EmployeeId
            || d.Declaration.TaxYear != date.Year || d.Declaration.Status != "Verified"
            || !d.Declaration.IsCurrentVerified || !d.Declaration.VerifiedAt.HasValue
            || d.Treatment.EmployeeId != x.EmployeeId || d.Treatment.TaxYear != date.Year
            || d.Treatment.EmployeeTaxDeclarationId != d.Declaration.EmployeeTaxDeclarationId
            || d.Treatment.RevisionNumber != d.Declaration.RevisionNumber)
            return Review("Exact selected Verified StandardSection40_1 declaration/treatment is required.");
        if (o is null || o.State is not ("ConfirmedZero" or "VerifiedAmount") || !o.VerifiedAt.HasValue
            || o.OpeningBalanceScope != "CurrentEmployer" || !o.CompletenessAttested
            || o.InputContractVersion != "PIT-TH-V1" || o.Currency != "THB"
            || o.AsOfDate >= date || (o.AsOfDate.Year != date.Year && o.AsOfDate != new DateOnly(date.Year - 1, 12, 31))
            || o.PriorTaxableEmploymentIncome is null or < 0 || o.PriorTaxWithheld is null or < 0
            || o.PriorSocialSecurityContribution is null or < 0
            || (o.State == "ConfirmedZero" && (o.PriorTaxableEmploymentIncome != 0 || o.PriorTaxWithheld != 0 || o.PriorSocialSecurityContribution != 0))
            || (o.AsOfDate.Year != date.Year && (o.PriorTaxableEmploymentIncome != 0 || o.PriorTaxWithheld != 0 || o.PriorSocialSecurityContribution != 0)))
            return Review("Reviewed complete CurrentEmployer opening balance/cutoff is required; Unknown is not zero.");
        var s = x.Schedule;
        if (s.ApplicablePaymentCount is null or < 1 or > 12 || s.PaymentOrdinal is null or < 1
            || s.PaymentOrdinal > s.ApplicablePaymentCount || string.IsNullOrWhiteSpace(s.Evidence)
            || !s.FullRegularPayment || s.RequiresLeaverReconciliation || s.RequiresYearEndReconciliation)
            return Review("Known regular monthly payment count/ordinal and full-payment schedule evidence are required; partial/leaver/year-end branches require review.");
        var current = ResolveRegular(x.CurrentLines);
        if (current.Amount is null) return Review(current.Error!);
        var priorIncome = o.PriorTaxableEmploymentIncome.Value;
        var histories = new List<PitHistoricalIncome>();
        foreach (var h in x.History)
        {
            if (h.EmployeeId != x.EmployeeId || h.Status != "Paid" || h.PayDate.Year != date.Year
                || h.PayDate <= o.AsOfDate || h.PayDate > date || h.EmployeePayrollId == x.CurrentPayrollId) continue;
            if (h.PayDate == date) return Review("Same-day historical/current income ownership requires review.");
            if (h.EmployeePayrollId == Guid.Empty || histories.Any(a => a.EmployeePayrollId == h.EmployeePayrollId))
                return Review("Historical payroll source identity is missing or duplicated.");
            var resolved = ResolveRegular(h.Lines);
            if (resolved.Amount is null) return Review("Historical " + resolved.Error);
            priorIncome = checked(priorIncome + resolved.Amount.Value); histories.Add(h);
        }
        if (!x.HistoricalWithholdingAuthoritative)
            return Review("Historical PIT withholding is not authoritative; D6E exact Paid PIT result ledger is required.");
        var priorWithheld = o.PriorTaxWithheld.Value;
        var prior = new List<PitPriorWithholding>();
        foreach (var h in x.PriorWithholding)
        {
            if (h.EmployeeId != x.EmployeeId || h.Status != "Paid" || h.PayDate.Year != date.Year
                || h.PayDate <= o.AsOfDate || h.PayDate > date || h.EmployeePayrollId == x.CurrentPayrollId) continue;
            if (h.PayDate == date) return Review("Same-day historical/current withholding ownership requires review.");
            if (h.PitResultId == Guid.Empty || h.EmployeePayrollId == Guid.Empty || h.Currency != "THB"
                || h.MethodVersion != "PIT-TH-V1" || h.Amount < 0 || TruncateSatang(h.Amount) != h.Amount
                || prior.Any(a => a.PitResultId == h.PitResultId || a.EmployeePayrollId == h.EmployeePayrollId))
                return Review("Prior PIT must have exact unique immutable result authority and nonnegative satang amounts.");
            priorWithheld = checked(priorWithheld + h.Amount); prior.Add(h);
        }
        if (histories.Any(h => !prior.Any(w => w.EmployeePayrollId == h.EmployeePayrollId && w.PayDate == h.PayDate))
            || prior.Any(w => !histories.Any(h => h.EmployeePayrollId == w.EmployeePayrollId && h.PayDate == w.PayDate))
            || TruncateSatang(priorWithheld) != priorWithheld)
            return Review("Paid income and PIT result history must reconcile by exact payroll/date; opening withholding must be satang-exact.");
        var sources = x.SsoSources;
        if (x.CurrentSsoStatus is not ("Resolved" or "NotApplicable")
            || sources.Count(z => z.SourceKind == PitSsoSourceKind.CurrentPayroll) != (x.CurrentSsoStatus == "Resolved" ? 1 : 0))
            return Review("Exact current employee D5 result or resolved NotApplicable SSO is required.");
        if (sources.Count(z => z.SourceKind == PitSsoSourceKind.OpeningBalance) != 1
            || sources.Count(z => z.SourceKind == PitSsoSourceKind.CurrentPayroll) > 1)
            return Review("Exact opening employee SSO and at most one current employee SSO source are required.");
        var openingSso = sources.Single(z => z.SourceKind == PitSsoSourceKind.OpeningBalance);
        if (openingSso.EmployeeTaxDeclarationId != d.Declaration.EmployeeTaxDeclarationId
            || openingSso.ThroughDate != o.AsOfDate || openingSso.EmployeeAmount != o.PriorSocialSecurityContribution
            || openingSso.EmployeePayrollId.HasValue || openingSso.EmployeePayrollStatutoryResultId.HasValue)
            return Review("Opening SSO must match the exact selected declaration opening.");
        var ids = new HashSet<Guid>(); var payrollIds = new HashSet<Guid>(); decimal sso = 0;
        foreach (var z in sources)
        {
            if (z.EmployeeAmount < 0) return Review("Employee SSO cannot be negative.");
            if (z.SourceKind != PitSsoSourceKind.OpeningBalance)
            {
                if (z.SourceKind is not (PitSsoSourceKind.HistoricalPaidPayroll or PitSsoSourceKind.CurrentPayroll)
                    || !z.EmployeePayrollId.HasValue || z.EmployeePayrollId == Guid.Empty
                    || !z.EmployeePayrollStatutoryResultId.HasValue || z.EmployeePayrollStatutoryResultId == Guid.Empty
                    || !ids.Add(z.EmployeePayrollStatutoryResultId.Value) || !payrollIds.Add(z.EmployeePayrollId.Value)
                    || z.ThroughDate <= o.AsOfDate || z.ThroughDate.Year != date.Year || z.ThroughDate > date
                    || (z.SourceKind == PitSsoSourceKind.HistoricalPaidPayroll && !histories.Any(h => h.EmployeePayrollId == z.EmployeePayrollId && h.PayDate == z.ThroughDate))
                    || (z.SourceKind == PitSsoSourceKind.CurrentPayroll && (z.ThroughDate != date || (x.CurrentPayrollId.HasValue && z.EmployeePayrollId != x.CurrentPayrollId))))
                    return Review("SSO source ownership/date/uniqueness does not match resolved current or Paid history.");
            }
            sso = checked(sso + z.EmployeeAmount);
        }
        if (c.EmploymentExpenseDeductionRate is null or < 0 or > 100 || c.EmploymentExpenseDeductionCap is null or <= 0
            || c.PersonalAllowanceAmount is null or < 0 || c.SpouseAllowanceAmount is null or < 0
            || c.ChildAllowanceAmount is null or < 0 || c.AdditionalChildAllowanceAmount is null or < 0
            || c.ParentAllowanceAmount is null or < 0 || c.AdoptedChildCombinedCountLimit is null or <= 0
            || c.MaximumEligibleParentCount is null or <= 0)
            return Review("Complete nonnegative typed PIT policy allowances/expense and positive limits are required.");
        int spouse = 0, lawful = 0, adopted = 0, additional = 0, parents = 0;
        foreach (var claim in d.Claims)
        {
            if (claim.EmployeeTaxClaimId == Guid.Empty || claim.Amount.HasValue || claim.Quantity is null or <= 0
                || string.IsNullOrWhiteSpace(claim.Reference)) return Review("Unsupported or incomplete claim facts require review; client Amount is not legal authority.");
            if (claim.ClaimType == "Spouse" && claim.Quantity == 1 && claim.ChildRelationshipType is null && !claim.AdditionalChildAllowanceEligible.HasValue) spouse++;
            else if (claim.ClaimType == "Parent" && claim.ChildRelationshipType is null && !claim.AdditionalChildAllowanceEligible.HasValue) parents = checked(parents + claim.Quantity.Value);
            else if (claim.ClaimType == "Child" && claim.AdditionalChildAllowanceEligible.HasValue)
            {
                if (claim.ChildRelationshipType == "Lawful") { lawful = checked(lawful + claim.Quantity.Value); if (claim.AdditionalChildAllowanceEligible.Value) additional = checked(additional + claim.Quantity.Value); }
                else if (claim.ChildRelationshipType == "Adopted" && !claim.AdditionalChildAllowanceEligible.Value) adopted = checked(adopted + claim.Quantity.Value);
                else return Review("Unsupported child category/additional allowance structure.");
            }
            else return Review("Unsupported claim structure requires review.");
        }
        if (d.Claims.Select(z => z.EmployeeTaxClaimId).Distinct().Count() != d.Claims.Count
            || spouse > 1 || parents > c.MaximumEligibleParentCount || d.TotalLivingLawfulChildren is < 0
            || (d.TotalLivingLawfulChildren.HasValue && lawful > d.TotalLivingLawfulChildren)
            || (adopted > 0 && (!d.TotalLivingLawfulChildren.HasValue
                || adopted > Math.Max(0, c.AdoptedChildCombinedCountLimit.Value - d.TotalLivingLawfulChildren.Value))))
            return Review("Claim quantities exceed reviewed policy capacity or declaration facts; no silent trimming.");
        var n = s.ApplicablePaymentCount.Value;
        var projected = checked(current.Amount.Value * n);
        var precap = checked(projected * c.EmploymentExpenseDeductionRate.Value / 100m);
        var expense = new PitExpenseCalculation(projected, c.EmploymentExpenseDeductionRate.Value, precap,
            c.EmploymentExpenseDeductionCap.Value, Math.Min(precap, c.EmploymentExpenseDeductionCap.Value));
        var allowance = new PitAllowanceCalculation(c.PersonalAllowanceAmount.Value, spouse * c.SpouseAllowanceAmount.Value,
            lawful, adopted, additional, checked((lawful + adopted) * c.ChildAllowanceAmount.Value),
            checked(additional * c.AdditionalChildAllowanceAmount.Value), parents, checked(parents * c.ParentAllowanceAmount.Value));
        var net = Math.Max(0, projected - expense.AllowedAmount - allowance.Personal - allowance.Spouse
            - allowance.OrdinaryChild - allowance.AdditionalChild - allowance.Parent - sso);
        var bands = c.Brackets;
        if (bands.Count == 0 || bands[0].LowerBoundInclusive != 0 || bands[^1].UpperBoundExclusive.HasValue)
            return Review("Bracket schedule must completely cover zero to infinity.");
        var breakdown = new List<PitBracketCalculation>(); decimal tax = 0;
        for (var i = 0; i < bands.Count; i++)
        {
            var b = bands[i];
            if (b.SortOrder != i + 1 || b.LowerBoundInclusive < 0 || b.Rate is < 0 or > 100
                || (b.UpperBoundExclusive.HasValue && b.UpperBoundExclusive <= b.LowerBoundInclusive)
                || (!b.UpperBoundExclusive.HasValue && i != bands.Count - 1)
                || (i > 0 && b.LowerBoundInclusive != bands[i - 1].UpperBoundExclusive))
                return Review("Invalid ordered/contiguous progressive bracket schedule.");
            var amount = Math.Max(0, Math.Min(net, b.UpperBoundExclusive ?? net) - b.LowerBoundInclusive);
            var bandTax = checked(amount * b.Rate / 100m); tax = checked(tax + bandTax);
            breakdown.Add(new(b.LowerBoundInclusive, b.UpperBoundExclusive, b.Rate, amount, bandTax));
        }
        var allocatable = TruncateSatang(tax); var rawAllocation = allocatable / n;
        var regular = TruncateSatang(rawAllocation); var final = s.PaymentOrdinal == n;
        var remaining = Math.Max(0, allocatable - priorWithheld);
        var withholding = final ? remaining : Math.Min(regular, remaining);
        var snapshot = new PitCalculationSnapshot("PIT-TH-V1-D6D-1", x, current.Amount.Value, priorIncome, n,
            projected, expense, allowance, sso, net, breakdown, tax, allocatable, tax - allocatable,
            rawAllocation, regular, priorWithheld, withholding, final ? withholding - regular : null,
            final, Math.Max(0, priorWithheld - allocatable));
        return new("Calculated", [], snapshot, JsonSerializer.Serialize(snapshot));
    }

    private (decimal? Amount, string? Error) ResolveRegular(IReadOnlyList<PitIncomeLine> lines)
    {
        var income = incomeResolver.Resolve(lines);
        if (income.Status != "Resolved") return (null, income.Message);
        if (lines.Any(z => z.ComponentType == "Earning" && z.PitIncomeTreatmentSnapshot == "Included"
            && z.PitPaymentTreatmentSnapshot != "Regular"))
            return (null, "Included Special/Unknown payment treatment requires review; unsupported income is not an exemption.");
        return (income.IncludedIncomeCandidate, null);
    }
    private static decimal TruncateSatang(decimal value) => decimal.Truncate(checked(value * 100m)) / 100m;
    private static PitCalculationResult Review(string reason) => new("RequiresReview", [reason]);
}
