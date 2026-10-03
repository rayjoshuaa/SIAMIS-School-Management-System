using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Payroll;

public sealed record PitIncomeLine(Guid PayrollComponentId, string ComponentCode, string ComponentName,
    string ComponentType, decimal Amount, string PitIncomeTreatmentSnapshot, string SourceType, Guid? SourceId, string PitPaymentTreatmentSnapshot = "Unknown")
{
    public static PitIncomeLine FromCalculatedLine(PayrollCalculatedLine line)
        => new(line.PayrollComponentId, line.ComponentCode, line.ComponentName, line.ComponentType,
            line.Amount, line.PitIncomeTreatmentSnapshot, line.SourceType, line.SourceId, line.PitPaymentTreatmentSnapshot);
    public static PitIncomeLine FromStoredLine(EmployeePayrollLineDto line)
        => new(line.PayrollComponentId, line.ComponentCode, line.ComponentName, line.ComponentType,
            line.Amount, line.PitIncomeTreatmentSnapshot, line.SourceType, line.SourceId, line.PitPaymentTreatmentSnapshot);
}
public sealed record PitIncomeIssue(int LineIndex, Guid PayrollComponentId, string ComponentCode,
    string ComponentName, string SourceType, Guid? SourceId, string Code, string Message);
/// <summary>Only Resolved carries a complete candidate. This is not legacy TaxableEarnings, net taxable income or tax.</summary>
public sealed record PitIncomeResolution(string Status, decimal? IncludedIncomeCandidate, string Message,
    IReadOnlyList<PitIncomeIssue> Issues);
public interface IPitIncomeResolver
{
    PitIncomeResolution Resolve(IReadOnlyList<PitIncomeLine> finalLines);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeTaxTreatmentRequest
{
    [Required, StringLength(20), RegularExpression("^(Unknown|Resident|NonResident)$")]
    public string ResidencyStatus { get; set; } = "Unknown";
    [Required, StringLength(30), RegularExpression("^(Unknown|StandardSection40_1|RequiresReview)$")]
    public string EmploymentTaxTreatment { get; set; } = "Unknown";
    /// <summary>Optional supporting declaration remarks. Omitted/null preserves existing remarks.</summary>
    [StringLength(2000)] public string? Remarks { get; set; }
}
/// <summary>Gregorian TaxYear. Verification and identity belong to the immutable declaration revision.</summary>
public sealed record EmployeeTaxTreatmentDto(Guid EmployeeTaxDeclarationId, Guid EmployeeId, int TaxYear,
    int RevisionNumber, string ResidencyStatus, string EmploymentTaxTreatment, string Status,
    DateTime? VerifiedAt, string? Remarks);
/// <summary>Approved only identifies verified standard treatment; no policy, income, history or withholding is resolved.</summary>
public sealed record EmployeeTaxTreatmentResolution(string Status, string Message, EmployeeTaxTreatmentDto? Treatment);
