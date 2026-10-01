using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Application.Payroll;

/// <summary>Historical V1 explanation. Rates are percentage points; input identifiers are snapshots.</summary>
public sealed record Section33CalculationSnapshot(
    int Version, string CalculationMethodVersion, string SchemeCode, string Jurisdiction,
    Guid StatutorySchemeId, Guid StatutoryPolicyVersionId, string PolicyVersion,
    DateOnly PolicyEffectiveFrom, DateOnly? PolicyEffectiveTo, string OfficialReference,
    Guid EmployeeStatutoryEnrollmentId, DateOnly EnrollmentEffectiveFrom, DateOnly? EnrollmentEffectiveTo,
    string Applicability, string InsuredPersonClassification, string ContributionMonth,
    DateOnly GoverningDate, string Currency, decimal ContributionWage, decimal ContributionBase,
    decimal MinimumBase, decimal MaximumBase, decimal EmployeeRate, decimal EmployerRate,
    decimal RawEmployeeAmount, decimal EmployeeAmount, decimal EmployerAmount, bool ZeroWage,
    string BaseSelectionMethod, string RoundingMethod, IReadOnlyList<Section33WageLine> WageInputs);

public sealed record Section33CalculationOutcome(string Status, string Message, Section33CalculationSnapshot? Snapshot = null);

public interface ISection33Calculator
{
    Section33CalculationOutcome Calculate(DateOnly start, DateOnly end, string currency,
        StatutorySchemeDto scheme, StatutoryEnrollmentResolution enrollment,
        StatutoryPolicyDto? policy, IReadOnlyList<Section33WageLine> lines);
}

public sealed record Section33PayrollIntegration(PayrollCalculationResult Calculation,
    EmployeePayrollStatutoryResult? Result);

public interface ISection33PayrollService
{
    Task<ServiceResult<Section33PayrollIntegration>> CalculateAsync(Guid employeeId, PayrollPeriod period,
        string currency, PayrollCalculationResult calculation, CancellationToken ct);
}

public sealed record EmployeePayrollStatutoryResultDto(Guid EmployeePayrollStatutoryResultId,
    Guid EmployeePayrollId, Guid StatutorySchemeId, Guid StatutoryPolicyVersionId,
    Guid EmployeeStatutoryEnrollmentId, string CalculationMethodVersion, string ContributionMonth,
    DateOnly GoverningDate, string Currency, decimal ContributionWage, decimal ContributionBase,
    decimal MinimumBase, decimal MaximumBase, decimal EmployeeRate, decimal EmployerRate,
    decimal RawEmployeeAmount, decimal EmployeeAmount, decimal EmployerAmount,
    string CalculationSnapshotJson, DateTime CreatedAt);
