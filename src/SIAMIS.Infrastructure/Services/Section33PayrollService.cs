using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Shared preview/generation orchestration; never saves data. EndDate represents the contribution month.</summary>
public sealed class Section33PayrollService(SIAMISDbContext db, IEmployeeStatutoryService enrollments,
    IStatutoryPolicyResolver policies, ISection33Calculator calculator) : ISection33PayrollService
{
    public async Task<ServiceResult<Section33PayrollIntegration>> CalculateAsync(Guid employeeId, PayrollPeriod period,
        string currency, PayrollCalculationResult calculation, CancellationToken ct)
    {
        var schemes = await db.StatutorySchemes.AsNoTracking().Where(x => x.Code == "TH-SSO-33").Take(2).ToListAsync(ct);
        if (schemes.Count != 1) return Fail("Section 33 requires exactly one configured TH-SSO-33 scheme; no applicability is inferred.");
        var scheme = schemes[0];
        var resolved = await enrollments.ResolveEnrollmentAsync(employeeId, scheme.StatutorySchemeId, period.EndDate, ct);
        if (!resolved.IsSuccess) return Fail(resolved.Failure!.Message);
        var enrollment = resolved.Value!;
        if (enrollment.Applicability == "NotApplicable")
            return ServiceResult<Section33PayrollIntegration>.Success(new(calculation, null));
        if (enrollment.Applicability != "Applicable") return Fail("Section 33 enrollment is Unknown or missing on PayrollPeriod.EndDate.");
        // Only final generated lines matter: preserve all existing Supplement/ReplaceAssignment semantics.
        if (calculation.Lines.Any(x => x.ComponentType == "Deduction" && x.ComponentCode == "DEDUCT-001"
            && x.SourceType is "Assignment" or "PayrollRule"))
            return Fail("DEDUCT-001 is reserved for automatic TH-SSO-33 employee contribution when SSO is Applicable. A final Assignment/PayrollRule deduction conflicts; no deduction was suppressed or added.");
        var policy = await policies.ResolveAsync(scheme.StatutorySchemeId, period.EndDate, ct);
        if (policy.Outcome != "Resolved") return Fail("Section 33 policy resolution failed: " + policy.Message);
        var outcome = calculator.Calculate(period.StartDate, period.EndDate, currency,
            new(scheme.StatutorySchemeId, scheme.Code, scheme.Name, scheme.Jurisdiction, scheme.SchemeType, scheme.IsActive, scheme.CreatedAt, scheme.UpdatedAt),
            enrollment, policy.Policy, calculation.Lines.Select(Section33WageLine.FromCalculatedLine).ToArray());
        if (outcome.Status != "Calculated") return Fail(outcome.Message);
        var s = outcome.Snapshot!;
        var result = new EmployeePayrollStatutoryResult {
            StatutorySchemeId = s.StatutorySchemeId, StatutoryPolicyVersionId = s.StatutoryPolicyVersionId,
            EmployeeStatutoryEnrollmentId = s.EmployeeStatutoryEnrollmentId, CalculationMethodVersion = s.CalculationMethodVersion,
            ContributionMonth = s.ContributionMonth, GoverningDate = s.GoverningDate, Currency = s.Currency,
            CalculationSnapshotJson = JsonSerializer.Serialize(s, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            SocialSecurity = new() { ContributionWage = s.ContributionWage, ContributionBase = s.ContributionBase,
                MinimumBase = s.MinimumBase, MaximumBase = s.MaximumBase, EmployeeRate = s.EmployeeRate, EmployerRate = s.EmployerRate,
                RawEmployeeAmount = s.RawEmployeeAmount, EmployeeAmount = s.EmployeeAmount, EmployerAmount = s.EmployerAmount }
        };
        // Zero results are historical evidence but do not create an AmountPositive-violating line.
        if (s.EmployeeAmount == 0) return ServiceResult<Section33PayrollIntegration>.Success(new(calculation, result));
        var components = await db.PayrollComponents.AsNoTracking().Where(x => x.Code == "DEDUCT-001").Take(2).ToListAsync(ct);
        if (components.Count != 1 || !components[0].IsActive || components[0].Category != "Deduction")
            return Fail("Automatic Section 33 deduction requires exactly one active Deduction component with code DEDUCT-001; no component was created or repaired.");
        var component = components[0];
        try {
            var deductions = checked(calculation.TotalDeductions + s.EmployeeAmount);
            if (deductions > 999_999_999_999_999.9999m || deductions > calculation.GrossPay)
                return Fail("Section 33 employee contribution would make deductions exceed GrossPay or the supported monetary range.");
            var line = new PayrollCalculatedLine(component.Id, component.Code!, component.Name, "Deduction", "Statutory",
                null, null, s.EmployeeRate, s.EmployeeAmount, "Automatic TH-SSO-33 employee contribution (SSO-TH-V1).",
                BaseType: "ContributionWage", BaseAmount: s.ContributionBase, MinimumBase: s.MinimumBase, MaximumBase: s.MaximumBase,
                SourceType: "Statutory", SourceId: result.EmployeePayrollStatutoryResultId,
                IsTaxableSnapshot: component.IsTaxable, IsStatutorySnapshot: component.IsStatutory,
                ContributionSideSnapshot: component.ContributionSide, SsoWageTreatmentSnapshot: component.SsoWageTreatment);
            return ServiceResult<Section33PayrollIntegration>.Success(new(calculation with {
                TotalDeductions = deductions, NetPay = calculation.GrossPay - deductions, Lines = [.. calculation.Lines, line] }, result));
        } catch (OverflowException) { return Fail("Section 33 payroll totals overflow; no payroll was saved."); }
    }

    public static EmployeePayrollStatutoryResultDto ToDto(EmployeePayrollStatutoryResult x)
    {
        var s = x.SocialSecurity;
        return new(x.EmployeePayrollStatutoryResultId, x.EmployeePayrollId, x.StatutorySchemeId, x.StatutoryPolicyVersionId,
            x.EmployeeStatutoryEnrollmentId, x.CalculationMethodVersion, x.ContributionMonth, x.GoverningDate, x.Currency,
            s.ContributionWage, s.ContributionBase, s.MinimumBase, s.MaximumBase, s.EmployeeRate, s.EmployerRate,
            s.RawEmployeeAmount, s.EmployeeAmount, s.EmployerAmount, x.CalculationSnapshotJson, x.CreatedAt);
    }
    private static ServiceResult<Section33PayrollIntegration> Fail(string message) => ServiceResult<Section33PayrollIntegration>.Fail("calculation", message);
}
