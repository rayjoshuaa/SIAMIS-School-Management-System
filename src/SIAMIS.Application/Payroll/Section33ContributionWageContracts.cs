namespace SIAMIS.Application.Payroll;

/// <summary>A final earning/deduction line's historical classification and provenance. Amounts are copied, not recalculated.</summary>
public sealed record Section33WageLine(
    Guid PayrollComponentId, string ComponentCode, string ComponentName, string ComponentType,
    decimal Amount, string SsoWageTreatmentSnapshot, string SourceType, Guid? SourceId)
{
    public static Section33WageLine FromCalculatedLine(PayrollCalculatedLine line)
        => new(line.PayrollComponentId, line.ComponentCode, line.ComponentName, line.ComponentType,
            line.Amount, line.SsoWageTreatmentSnapshot, line.SourceType, line.SourceId);

    public static Section33WageLine FromStoredLine(EmployeePayrollLineDto line)
        => new(line.PayrollComponentId, line.ComponentCode, line.ComponentName, line.ComponentType,
            line.Amount, line.SsoWageTreatmentSnapshot, line.SourceType, line.SourceId);
}

public sealed record Section33WageIssue(
    int LineIndex, Guid PayrollComponentId, string ComponentCode, string ComponentName,
    string SourceType, Guid? SourceId, string Code, string Message);

/// <summary>Only Resolved has a wage candidate, including an explicitly resolved zero. No base or contribution amount is calculated.</summary>
public sealed record Section33ContributionWageResult(
    string Status, decimal? ContributionWageCandidate, string Message,
    IReadOnlyList<Section33WageIssue> Issues);

public interface ISection33ContributionWageResolver
{
    /// <summary>Consumes the complete final line set once, after suppression/ordering. Applicability is supplied by a future authorized enrollment resolution; no date is selected here.</summary>
    Section33ContributionWageResult Resolve(string applicability, IReadOnlyList<Section33WageLine> finalLines);
}
