using System.Text.Json.Serialization;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Application.Payroll;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PitSsoSourceKind { OpeningBalance, HistoricalPaidPayroll, CurrentPayroll }

/// <summary>Unaggregated historical input for a future PIT snapshot. No tax or SSO calculation.</summary>
public sealed record PitSsoSource(PitSsoSourceKind SourceKind, decimal EmployeeAmount, DateOnly ThroughDate,
    Guid? EmployeePayrollStatutoryResultId, Guid? EmployeePayrollId, Guid? EmployeeTaxDeclarationId);

public sealed record PitSsoRecognitionMetadata(string Mode, string HistoricalPayrollStatus,
    bool IncludesCurrentPayrollEmployeeAmount, bool ProjectsFutureContributions, bool HasIndependentPitCap);

/// <summary>PIT-TH-V1 recognition contract only; not called by payroll calculation/generation.</summary>
public static class PitSsoRecognitionContract
{
    public const string MethodVersion = "PIT-TH-V1";
    public static PitSsoRecognitionMetadata Metadata { get; } = new("ActualCumulative", "Paid", true, false, false);

    public static PitSsoSource Opening(EmployeeTaxDeclaration declaration, EmployeeTaxDeclarationSelection selection)
    {
        var opening = declaration.OpeningBalance;
        if (declaration.Status != "Verified" || selection.CurrentDeclarationId != declaration.EmployeeTaxDeclarationId
            || selection.EmployeeId != declaration.EmployeeId || selection.TaxYear != declaration.TaxYear
            || opening is null || opening.EmployeeTaxDeclarationId != declaration.EmployeeTaxDeclarationId)
            throw new InvalidOperationException("Opening SSO must belong to the exact selected Verified declaration.");
        if (opening.InputContractVersion != MethodVersion || opening.OpeningBalanceScope != "CurrentEmployer"
            || !opening.CompletenessAttested || opening.State is not ("ConfirmedZero" or "VerifiedAmount")
            || !opening.VerifiedAt.HasValue || opening.Currency != "THB"
            || opening.PriorSocialSecurityContribution is null or < 0)
            throw new InvalidOperationException("A reviewed, complete CurrentEmployer opening statement is required; Unknown is unresolved.");
        return new(PitSsoSourceKind.OpeningBalance, opening.PriorSocialSecurityContribution.Value,
            opening.AsOfDate, null, null, opening.EmployeeTaxDeclarationId);
    }

    public static PitSsoSource? Historical(EmployeePayroll payroll, DateOnly payDate,
        EmployeePayrollStatutoryResult result, DateOnly openingCutoff, DateOnly currentPayDate)
    {
        // Dates are payroll business PayDates, not SSO governing EndDates or PaidAt UTC timestamps.
        if (payroll.Status != "Paid" || payDate <= openingCutoff || payDate > currentPayDate
            || payDate.Year != currentPayDate.Year) return null;
        if (payDate == currentPayDate)
            throw new InvalidOperationException("Same-day historical/current ownership requires review; no partial-day ordering is assumed.");
        if (result.EmployeePayrollId != payroll.EmployeePayrollId)
            throw new InvalidOperationException("D5 result does not belong to the historical payroll.");
        return Source(PitSsoSourceKind.HistoricalPaidPayroll, result, payDate);
    }

    public static PitSsoSource Current(EmployeePayrollStatutoryResult result, DateOnly payDate, DateOnly openingCutoff)
    {
        if (payDate <= openingCutoff)
            throw new InvalidOperationException("Current payroll overlaps the inclusive opening cutoff.");
        // Current transaction output need not already be Paid; no historical status inference.
        return Source(PitSsoSourceKind.CurrentPayroll, result, payDate);
    }

    private static PitSsoSource Source(PitSsoSourceKind kind, EmployeePayrollStatutoryResult result, DateOnly date)
    {
        if (result.Currency != "THB" || result.CalculationMethodVersion != "SSO-TH-V1" || result.EmployeePayrollStatutoryResultId == Guid.Empty
            || result.EmployeePayrollId == Guid.Empty || result.SocialSecurity is null
            || result.SocialSecurity.EmployeePayrollStatutoryResultId != result.EmployeePayrollStatutoryResultId
            || result.SocialSecurity.EmployeeAmount < 0)
            throw new InvalidOperationException("An exact valid employee-side D5 result is required.");
        return new(kind, result.SocialSecurity.EmployeeAmount, date,
            result.EmployeePayrollStatutoryResultId, result.EmployeePayrollId, null);
    }
}
