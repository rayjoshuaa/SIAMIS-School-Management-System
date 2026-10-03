using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Shared read-only resolution/calculation. Generation owns persistence and its employee transaction.</summary>
public sealed class PitPayrollService(SIAMISDbContext db, IEmployeeStatutoryService employeeInputs,
    IStatutoryPolicyResolver policies, IPitPaymentScheduleService schedules, IPitCalculator calculator) : IPitPayrollService
{
    public static readonly Guid ComponentId = Guid.Parse("f1000000-0000-0000-0000-000000000002");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<ServiceResult<PitPayrollIntegration>> CalculateAsync(Guid employeeId, PayrollPeriod period,
        Guid intendedPayrollId, Guid? replacedPayrollId, BasicSalaryCalculationSnapshot salary,
        PayrollCalculationResult calculation, EmployeePayrollStatutoryResult? sso, CancellationToken ct)
    {
        // Statutory ownership applies even when enrollment explicitly opts out.
        if (calculation.Lines.Any(x => x.PayrollComponentId == ComponentId || x.ComponentCode == "DEDUCT-002"))
            return Review("DEDUCT-002 is PIT-owned and cannot originate from an assignment or PayrollRule.");
        var scheme = await db.StatutorySchemes.AsNoTracking().SingleOrDefaultAsync(x => x.Code == "TH-PIT", ct);
        if (scheme is null || !scheme.IsActive || scheme.SchemeType != "PersonalIncomeTax" || scheme.Jurisdiction != "TH")
            return Review("PIT applicability is Unknown: an active TH-PIT PersonalIncomeTax scheme is required.");
        var enrollment = await employeeInputs.ResolveEnrollmentAsync(employeeId, scheme.StatutorySchemeId, period.PayDate, ct);
        if (!enrollment.IsSuccess || enrollment.Value?.Applicability is null)
            return Review(enrollment.Failure?.Message ?? "PIT applicability is unresolved.");
        if (enrollment.Value.Applicability == "NotApplicable")
            return ServiceResult<PitPayrollIntegration>.Success(new(calculation, null, new("NotApplicable", ["Explicit PIT NotApplicable enrollment on PayDate; no result or deduction."])));
        if (enrollment.Value.Applicability != "Applicable") return Review("PIT applicability is Unknown on PayDate; explicit enrollment is required.");
        var component = await db.PayrollComponents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ComponentId, ct);
        if (component is null || component.Code != "DEDUCT-002" || !component.IsActive || component.Category != "Deduction")
            return Review("PIT requires the active canonical Deduction component f1000000-0000-0000-0000-000000000002 / DEDUCT-002.");
        if (salary.Currency != "THB") return Review("PIT-TH-V1 requires THB payroll inputs.");
        var selected = await schedules.ResolveAsync(employeeId, period.PayDate, ct);
        if (selected.Status != "Resolved") return Review(selected.Message);
        var policy = await policies.ResolveAsync(scheme.StatutorySchemeId, period.PayDate, ct);
        if (policy.Outcome != "Resolved" || policy.Policy is null || policy.Scheme is null) return Review(policy.Message);
        var declarationId = await db.EmployeeTaxDeclarationSelections.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.TaxYear == period.PayDate.Year)
            .Select(x => (Guid?)x.CurrentDeclarationId).SingleOrDefaultAsync(ct);
        if (declarationId is null) return Review("No selected Verified PIT declaration covers the PayDate tax year.");
        var declaration = (await employeeInputs.GetDeclarationAsync(employeeId, declarationId.Value, ct)).Value;
        if (declaration?.OpeningBalance?.PriorSocialSecurityContribution is null) return Review("Reviewed opening employee SSO and declaration are required.");
        var opening = declaration.OpeningBalance;
        // Detect unsupported employment facts without deriving annual payment count or ordinal from D3.
        var schedule = selected.Payment! with
        {
            FullRegularPayment = salary.FullMonthEntitlement && salary.Segments.Select(x => x.EmploymentRecordId).Distinct().Count() == 1,
            RequiresLeaverReconciliation = salary.Segments.Any(x => x.EmploymentEnd.HasValue && x.EmploymentEnd.Value <= period.EndDate)
        };
        var paid = await db.EmployeePayrolls.AsNoTracking().Include(x => x.PayrollPeriod).Include(x => x.Lines)
            .Where(x => x.EmployeeId == employeeId && x.EmployeePayrollId != replacedPayrollId && x.PayrollPeriodId != period.PayrollPeriodId
                && x.Status == "Paid" && x.PayrollPeriod.PayDate.Year == period.PayDate.Year
                && x.PayrollPeriod.PayDate > opening.AsOfDate && x.PayrollPeriod.PayDate <= period.PayDate)
            .OrderBy(x => x.PayrollPeriod.PayDate).ThenBy(x => x.EmployeePayrollId).ToListAsync(ct);
        var paidIds = paid.Select(x => x.EmployeePayrollId).ToArray();
        var historicalPit = await db.EmployeePayrollPitResults.AsNoTracking().Where(x => paidIds.Contains(x.EmployeePayrollId)).ToListAsync(ct);
        var historicalSso = await db.EmployeePayrollStatutoryResults.AsNoTracking().Include(x => x.SocialSecurity)
            .Where(x => paidIds.Contains(x.EmployeePayrollId) && x.CalculationMethodVersion == "SSO-TH-V1").ToListAsync(ct);
        var sources = new List<PitSsoSource> { new(PitSsoSourceKind.OpeningBalance, opening.PriorSocialSecurityContribution.Value,
            opening.AsOfDate, null, null, declarationId) };
        sources.AddRange(historicalSso.Select(x => new PitSsoSource(PitSsoSourceKind.HistoricalPaidPayroll, x.SocialSecurity.EmployeeAmount,
            paid.Single(p => p.EmployeePayrollId == x.EmployeePayrollId).PayrollPeriod.PayDate, x.EmployeePayrollStatutoryResultId, x.EmployeePayrollId, null)));
        if (sso is not null) sources.Add(new(PitSsoSourceKind.CurrentPayroll, sso.SocialSecurity.EmployeeAmount, period.PayDate,
            sso.EmployeePayrollStatutoryResultId, intendedPayrollId, null));
        var input = new PitCalculationInput(employeeId, period.PayrollPeriodId, intendedPayrollId, period.PayDate,
            policy.Scheme, policy.Policy, declaration, schedule, calculation.Lines.Select(PitIncomeLine.FromCalculatedLine).ToArray(),
            paid.Select(x => new PitHistoricalIncome(x.EmployeePayrollId, x.EmployeeId, x.PayrollPeriod.PayDate, x.Status,
                x.Lines.Select(l => new PitIncomeLine(l.PayrollComponentId, l.ComponentCode, l.ComponentName, l.ComponentType,
                    l.Amount, l.PitIncomeTreatmentSnapshot, l.SourceType, l.SourceId, l.PitPaymentTreatmentSnapshot)).ToArray())).ToArray(),
            historicalPit.Select(x => new PitPriorWithholding(x.EmployeePayrollPitResultId, x.EmployeePayrollId, x.EmployeeId,
                x.GoverningDate, "Paid", x.Currency, x.CalculationMethodVersion, x.CurrentWithholding)).ToArray(),
            sources, true, sso is null ? "NotApplicable" : "Resolved");
        var result = calculator.Calculate(input);
        if (result.Status != "Calculated" || result.Calculation is null) return Review(string.Join(" ", result.Reasons));
        var c = result.Calculation;
        var evidence = new PitPayrollSnapshot("D6E-V1", enrollment.Value.Enrollment!.EmployeeStatutoryEnrollmentId, selected.Schedule!, c);
        var row = new EmployeePayrollPitResult { EmployeePayrollId = intendedPayrollId, EmployeeId = employeeId,
            PayrollPeriodId = period.PayrollPeriodId, GoverningDate = period.PayDate, TaxYear = period.PayDate.Year,
            StatutorySchemeId = scheme.StatutorySchemeId, StatutoryPolicyVersionId = policy.Policy.StatutoryPolicyVersionId,
            EmployeeStatutoryEnrollmentId = evidence.EmployeeStatutoryEnrollmentId, EmployeeTaxDeclarationId = declarationId.Value,
            EmployeePitPaymentScheduleId = selected.Schedule!.EmployeePitPaymentScheduleId, ScheduleRevisionNumber = selected.Schedule.RevisionNumber,
            ApplicablePaymentCount = c.ApplicablePaymentCount, PaymentOrdinal = schedule.PaymentOrdinal!.Value,
            CurrentRegularIncome = c.CurrentRegularIncome, PriorRecognizedIncome = c.PriorRecognizedIncome, ProjectedRegularIncome = c.ProjectedRegularIncome,
            EmploymentExpenseDeduction = c.Expense.AllowedAmount, PersonalAllowance = c.Allowances.Personal, SpouseAllowance = c.Allowances.Spouse,
            ChildAllowance = c.Allowances.OrdinaryChild + c.Allowances.AdditionalChild, ParentAllowance = c.Allowances.Parent,
            RecognizedEmployeeSso = c.RecognizedEmployeeSso, NetTaxableIncome = c.NetTaxableIncome, RawAnnualTax = c.RawAnnualTax,
            AllocatableAnnualWithholding = c.AllocatableAnnualWithholding, SubSatangRemainder = c.SubSatangRemainder,
            PriorRecognizedWithholding = c.PriorRecognizedWithholding, CurrentWithholding = c.CurrentWithholding,
            FinalAllocationResidual = c.FinalAllocationResidual, IsFinalScheduledPayment = c.IsFinalScheduledPayment,
            OverWithheldAmount = c.OverWithheldAmount, CalculationSnapshotJson = JsonSerializer.Serialize(evidence, JsonOptions) };
        var deductions = calculation.TotalDeductions + c.CurrentWithholding;
        if (deductions > calculation.GrossPay) return Review("PIT withholding would exceed the available GrossPay after deductions; no negative NetPay is persisted.");
        var lines = calculation.Lines.ToList();
        if (c.CurrentWithholding > 0) lines.Add(new(component.Id, component.Code!, component.Name, "Deduction", "Statutory", null,
            null, null, c.CurrentWithholding, "PIT-TH-V1 employee withholding", SourceType: "Statutory", SourceId: row.EmployeePayrollPitResultId,
            IsTaxableSnapshot: component.IsTaxable, IsStatutorySnapshot: true, ContributionSideSnapshot: "Employee"));
        return ServiceResult<PitPayrollIntegration>.Success(new(calculation with { TotalDeductions = deductions,
            NetPay = calculation.GrossPay - deductions, Lines = lines }, row, new("Calculated", result.Reasons, evidence)));
    }
    public async Task<ServiceResult<EmployeePayrollPitResultDto?>> GetResultAsync(Guid payrollId, CancellationToken ct)
    {
        if (!await db.EmployeePayrolls.AsNoTracking().AnyAsync(x => x.EmployeePayrollId == payrollId, ct))
            return ServiceResult<EmployeePayrollPitResultDto?>.Fail("not_found", "Payroll was not found.");
        return ServiceResult<EmployeePayrollPitResultDto?>.Success(await db.EmployeePayrollPitResults.AsNoTracking().Where(x => x.EmployeePayrollId == payrollId)
            .Select(x => new EmployeePayrollPitResultDto(x.EmployeePayrollPitResultId, x.EmployeePayrollId, x.EmployeeId, x.PayrollPeriodId,
                x.GoverningDate, x.TaxYear, x.Currency, x.CalculationMethodVersion, x.CurrentWithholding, x.CalculationSnapshotJson, x.CreatedAt)).SingleOrDefaultAsync(ct));
    }
    private static ServiceResult<PitPayrollIntegration> Review(string message) => ServiceResult<PitPayrollIntegration>.Fail("requires_review", "PIT RequiresReview: " + message);
}
