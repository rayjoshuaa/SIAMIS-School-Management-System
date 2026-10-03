using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Read-only resolver. Schedule authority and PIT result ledger are never invented.</summary>
public sealed class PitCalculationPreviewService(SIAMISDbContext db, IStatutoryPolicyResolver policies,
    IEmployeeStatutoryService declarations, IPayrollPreviewService payrollPreview,
    IPitCalculator calculator) : IPitCalculationPreviewService
{
    public async Task<ServiceResult<PitCalculationResult>> PreviewAsync(Guid employeeId, Guid payrollPeriodId, CancellationToken ct)
    {
        if (!await db.Employees.AsNoTracking().AnyAsync(z => z.EmployeeId == employeeId, ct))
            return ServiceResult<PitCalculationResult>.Fail("not_found", "Employee was not found.");
        var period = await db.PayrollPeriods.AsNoTracking().SingleOrDefaultAsync(z => z.PayrollPeriodId == payrollPeriodId, ct);
        if (period is null) return ServiceResult<PitCalculationResult>.Fail("not_found", "Payroll period was not found.");
        var reasons = new List<string>();
        var schemeId = await db.StatutorySchemes.AsNoTracking().Where(z => z.Code == "TH-PIT")
            .Select(z => (Guid?)z.StatutorySchemeId).SingleOrDefaultAsync(ct);
        StatutoryPolicyResolution? policy = null;
        if (schemeId.HasValue) policy = await policies.ResolveAsync(schemeId.Value, period.PayDate, ct);
        if (policy?.Outcome != "Resolved") reasons.Add(policy?.Message ?? "No TH-PIT scheme/policy is available for PayDate.");
        var declarationId = await db.EmployeeTaxDeclarationSelections.AsNoTracking()
            .Where(z => z.EmployeeId == employeeId && z.TaxYear == period.PayDate.Year)
            .Select(z => (Guid?)z.CurrentDeclarationId).SingleOrDefaultAsync(ct);
        EmployeeTaxDeclarationDto? declaration = null;
        if (declarationId.HasValue) declaration = (await declarations.GetDeclarationAsync(employeeId, declarationId.Value, ct)).Value;
        if (declaration is null) reasons.Add("No selected Verified declaration is available for the PayDate tax year.");
        else if (EmployeeTaxTreatmentResolver.Resolve(declaration.Treatment).Status != "Approved")
            reasons.Add("Selected employee tax treatment requires review.");
        var cutoff = declaration?.OpeningBalance?.AsOfDate;
        var hasHistory = await db.EmployeePayrolls.AsNoTracking().AnyAsync(z => z.EmployeeId == employeeId && z.Status == "Paid"
            && z.PayrollPeriodId != payrollPeriodId && z.PayrollPeriod.PayDate.Year == period.PayDate.Year
            && (!cutoff.HasValue || z.PayrollPeriod.PayDate > cutoff) && z.PayrollPeriod.PayDate <= period.PayDate, ct);
        if (hasHistory) reasons.Add("Prior Paid SIAMIS PIT history is not authoritative: D6E immutable PIT result ledger is not yet available.");
        // PayrollPeriod dates and D3 payable days do not define an annual payment schedule.
        reasons.Add("Applicable annual monthly payment count and current/final scheduled ordinal cannot be proven from existing SIAMIS schedule facts; RequiresReview.");
        if (policy?.Policy is null || declaration is null || hasHistory)
            return Review(reasons);
        var preview = await payrollPreview.PreviewAsync(payrollPeriodId, new() { EmployeeIds = [employeeId] }, ct);
        var current = preview.Value?.Results.SingleOrDefault();
        if (!preview.IsSuccess || current?.Status != "Calculated")
        {
            reasons.Add(preview.Failure?.Message ?? current?.Message ?? "Current payroll input could not be resolved.");
            return Review(reasons);
        }
        var opening = declaration.OpeningBalance;
        if (opening is null || opening.PriorSocialSecurityContribution is null)
        { reasons.Add("Reviewed opening employee SSO is unresolved."); return Review(reasons); }
        var sso = new List<PitSsoSource> { new(PitSsoSourceKind.OpeningBalance,
            opening.PriorSocialSecurityContribution.Value, opening.AsOfDate, null, null, declarationId) };
        if (current.SocialSecurity is { } result)
            sso.Add(new(PitSsoSourceKind.CurrentPayroll, result.EmployeeAmount, period.PayDate,
                result.EmployeePayrollStatutoryResultId, result.EmployeePayrollId, null));
        var input = new PitCalculationInput(employeeId, payrollPeriodId, current.SocialSecurity?.EmployeePayrollId,
            period.PayDate, policy.Scheme!, policy.Policy, declaration, new(null, null, null),
            current.Lines.Select(z => new PitIncomeLine(z.PayrollComponentId, z.ComponentCode, z.ComponentName,
                z.ComponentType, z.Amount, z.PitIncomeTreatmentSnapshot, z.SourceType, z.SourceId, z.PitPaymentTreatmentSnapshot)).ToArray(),
            [], [], sso, true, current.SocialSecurity is null ? "NotApplicable" : "Resolved");
        return ServiceResult<PitCalculationResult>.Success(calculator.Calculate(input));
    }
    private static ServiceResult<PitCalculationResult> Review(IReadOnlyList<string> reasons)
        => ServiceResult<PitCalculationResult>.Success(new("RequiresReview", reasons));
}
