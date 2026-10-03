using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

/// <summary>Resolved schedule facts, not client-selected preview parameters. Null facts require review.</summary>
public sealed record PitPaymentSchedule(int? ApplicablePaymentCount, int? PaymentOrdinal,
    string? Evidence, bool FullRegularPayment = true, bool RequiresLeaverReconciliation = false,
    bool RequiresYearEndReconciliation = false);
/// <summary>Immutable historical line snapshots. Recognition uses owning payroll status and business PayDate.</summary>
public sealed record PitHistoricalIncome(Guid EmployeePayrollId, Guid EmployeeId, DateOnly PayDate,
    string Status, IReadOnlyList<PitIncomeLine> Lines);
/// <summary>Future D6E result authority; never a generic deduction-line amount.</summary>
public sealed record PitPriorWithholding(Guid PitResultId, Guid EmployeePayrollId, Guid EmployeeId,
    DateOnly PayDate, string Status, string Currency, string MethodVersion, decimal Amount);
public sealed record PitCalculationInput(Guid EmployeeId, Guid PayrollPeriodId, Guid? CurrentPayrollId,
    DateOnly GoverningDate, StatutorySchemeDto Scheme, StatutoryPolicyDto Policy,
    EmployeeTaxDeclarationDto Declaration, PitPaymentSchedule Schedule,
    IReadOnlyList<PitIncomeLine> CurrentLines, IReadOnlyList<PitHistoricalIncome> History,
    IReadOnlyList<PitPriorWithholding> PriorWithholding, IReadOnlyList<PitSsoSource> SsoSources,
    bool HistoricalWithholdingAuthoritative, string CurrentSsoStatus);
public sealed record PitExpenseCalculation(decimal IncomeBasis, decimal Rate, decimal PreCapAmount,
    decimal Cap, decimal AllowedAmount);
public sealed record PitAllowanceCalculation(decimal Personal, decimal Spouse, int LawfulChildren,
    int AdoptedChildren, int AdditionalChildren, decimal OrdinaryChild, decimal AdditionalChild,
    int Parents, decimal Parent);
public sealed record PitBracketCalculation(decimal LowerBoundInclusive, decimal? UpperBoundExclusive,
    decimal Rate, decimal TaxableAmount, decimal TaxAmount);
/// <summary>All identities/input facts are retained in Input; no timestamps or randomly generated result IDs.</summary>
public sealed record PitCalculationSnapshot(string SnapshotVersion, PitCalculationInput Input,
    decimal CurrentRegularIncome, decimal PriorRecognizedIncome, int ApplicablePaymentCount,
    decimal ProjectedRegularIncome, PitExpenseCalculation Expense, PitAllowanceCalculation Allowances,
    decimal RecognizedEmployeeSso, decimal NetTaxableIncome, IReadOnlyList<PitBracketCalculation> Brackets,
    decimal RawAnnualTax, decimal AllocatableAnnualWithholding, decimal SubSatangRemainder,
    decimal RawRegularAllocation, decimal RegularWithholdingAmount, decimal PriorRecognizedWithholding,
    decimal CurrentWithholding, decimal? FinalAllocationResidual, bool IsFinalScheduledPayment,
    decimal OverWithheldAmount);
public sealed record PitCalculationResult(string Status, IReadOnlyList<string> Reasons,
    PitCalculationSnapshot? Calculation = null, string? CalculationSnapshotJson = null);
public interface IPitCalculator { PitCalculationResult Calculate(PitCalculationInput input); }
public interface IPitCalculationPreviewService
{
    Task<ServiceResult<PitCalculationResult>> PreviewAsync(Guid employeeId, Guid payrollPeriodId, CancellationToken ct);
}
