using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StatutoryEnrollmentRequest
{
    [Required] public Guid? StatutorySchemeId { get; set; }
    [Required] public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    [Required, StringLength(20)] public string Applicability { get; set; } = string.Empty;
    [StringLength(100)] public string? MembershipNumber { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StatutoryEnrollmentEndRequest
{
    [Required] public DateOnly? EffectiveTo { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeTaxProfileRequest
{
    [StringLength(100)] public string? TaxpayerIdentificationNumber { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeTaxDeclarationCreateRequest
{
    [Required, Range(1, 9999)] public int? TaxYear { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeTaxDeclarationUpdateRequest
{
    [StringLength(2000)] public string? Remarks { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeTaxClaimRequest
{
    [Required, StringLength(20)] public string ClaimType { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? Amount { get; set; }
    [Range(1, int.MaxValue)] public int? Quantity { get; set; }
    [StringLength(500)] public string? Reference { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeTaxOpeningBalanceRequest
{
    [Required, StringLength(20)] public string State { get; set; } = string.Empty;
    [Required, StringLength(3)] public string Currency { get; set; } = "THB";
    public decimal? PriorTaxableEmploymentIncome { get; set; }
    public decimal? PriorTaxWithheld { get; set; }
    public decimal? PriorSocialSecurityContribution { get; set; }
    [Required] public DateOnly? AsOfDate { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
}

// Membership is omitted from list DTOs; obtain it only from employee-scoped detail.
public sealed record StatutoryEnrollmentSummaryDto(Guid EmployeeStatutoryEnrollmentId, Guid EmployeeId,
    Guid StatutorySchemeId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Applicability,
    DateTime CreatedAt, DateTime UpdatedAt);
public sealed record StatutoryEnrollmentDto(Guid EmployeeStatutoryEnrollmentId, Guid EmployeeId,
    Guid StatutorySchemeId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Applicability,
    string? MembershipNumber, string? Remarks, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record StatutoryEnrollmentResolution(string Applicability, string Message, StatutoryEnrollmentSummaryDto? Enrollment);
public sealed record EmployeeTaxProfileDto(Guid EmployeeTaxProfileId, Guid EmployeeId,
    string? TaxpayerIdentificationNumber, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record EmployeeTaxClaimDto(Guid EmployeeTaxClaimId, string ClaimType, decimal? Amount,
    int? Quantity, string? Reference, string? Remarks, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record EmployeeTaxOpeningBalanceDto(string State, string Currency,
    decimal? PriorTaxableEmploymentIncome, decimal? PriorTaxWithheld, decimal? PriorSocialSecurityContribution,
    DateOnly AsOfDate, string? Remarks, DateTime? VerifiedAt, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record EmployeeTaxDeclarationSummaryDto(Guid EmployeeTaxDeclarationId, Guid EmployeeId,
    int TaxYear, int RevisionNumber, Guid? ReplacesDeclarationId, string Status, bool IsCurrentVerified,
    DateTime? VerifiedAt, string? Remarks, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record EmployeeTaxDeclarationDto(EmployeeTaxDeclarationSummaryDto Declaration,
    string? TaxpayerIdentificationNumberSnapshot, IReadOnlyList<EmployeeTaxClaimDto> Claims,
    EmployeeTaxOpeningBalanceDto? OpeningBalance);

public interface IEmployeeStatutoryService
{
    Task<ServiceResult<IReadOnlyList<StatutoryEnrollmentSummaryDto>>> ListEnrollmentsAsync(Guid employeeId, CancellationToken ct);
    Task<ServiceResult<StatutoryEnrollmentDto>> GetEnrollmentAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<ServiceResult<StatutoryEnrollmentDto>> CreateEnrollmentAsync(Guid employeeId, StatutoryEnrollmentRequest request, CancellationToken ct);
    Task<ServiceResult<StatutoryEnrollmentDto>> EndEnrollmentAsync(Guid employeeId, Guid id, StatutoryEnrollmentEndRequest request, CancellationToken ct);
    Task<ServiceResult<StatutoryEnrollmentResolution>> ResolveEnrollmentAsync(Guid employeeId, Guid schemeId, DateOnly date, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxProfileDto?>> GetProfileAsync(Guid employeeId, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxProfileDto>> SetProfileAsync(Guid employeeId, EmployeeTaxProfileRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<EmployeeTaxDeclarationSummaryDto>>> ListDeclarationsAsync(Guid employeeId, int? taxYear, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxDeclarationDto>> GetDeclarationAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxDeclarationDto>> CreateDeclarationAsync(Guid employeeId, EmployeeTaxDeclarationCreateRequest request, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxDeclarationDto>> UpdateDeclarationAsync(Guid employeeId, Guid id, EmployeeTaxDeclarationUpdateRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteDeclarationAsync(Guid employeeId, Guid id, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxDeclarationDto>> SetClaimAsync(Guid employeeId, Guid declarationId, Guid? claimId, EmployeeTaxClaimRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteClaimAsync(Guid employeeId, Guid declarationId, Guid claimId, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxDeclarationDto>> SetOpeningAsync(Guid employeeId, Guid declarationId, EmployeeTaxOpeningBalanceRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteOpeningAsync(Guid employeeId, Guid declarationId, CancellationToken ct);
    Task<ServiceResult<EmployeeTaxDeclarationDto>> VerifyDeclarationAsync(Guid employeeId, Guid id, CancellationToken ct);
}
