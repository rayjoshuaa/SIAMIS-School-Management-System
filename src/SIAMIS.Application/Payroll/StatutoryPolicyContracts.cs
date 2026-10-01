using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StatutorySchemeRequest
{
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(2)] public string Jurisdiction { get; set; } = "TH";
    [Required, StringLength(30)] public string SchemeType { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class StatutoryPolicyRequest
{
    [Required, StringLength(50)] public string Version { get; set; } = string.Empty;
    [Required] public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    [Required, StringLength(3)] public string Currency { get; set; } = "THB";
    [StringLength(2000)] public string? OfficialReference { get; set; }
    [StringLength(50)] public string? CalculationMethodVersion { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StatutoryPolicyCreateRequest : StatutoryPolicyRequest
{
    [Required] public Guid? StatutorySchemeId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SocialSecurityPolicyRequest
{
    public decimal? EmployeeContributionRate { get; set; }
    public decimal? EmployerContributionRate { get; set; }
    public decimal? MinimumContributionBase { get; set; }
    public decimal? MaximumContributionBase { get; set; }
    [StringLength(50)] public string? InsuredPersonClassification { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PitPolicyRequest
{
    [Range(1, 9999)] public int? TaxYear { get; set; }
    public decimal? EmploymentExpenseDeductionRate { get; set; }
    public decimal? EmploymentExpenseDeductionCap { get; set; }
    public decimal? PersonalAllowanceAmount { get; set; }
    [StringLength(50)] public string? WithholdingMethodIdentifier { get; set; }
    public IReadOnlyList<PitTaxBracketRequest> Brackets { get; set; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PitTaxBracketRequest
{
    [Required, Range(1, int.MaxValue)] public int? SortOrder { get; set; }
    [Required] public decimal? LowerBoundInclusive { get; set; }
    public decimal? UpperBoundExclusive { get; set; }
    [Required] public decimal? Rate { get; set; }
}

public sealed record StatutorySchemeDto(Guid StatutorySchemeId, string Code, string Name, string Jurisdiction,
    string SchemeType, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record SocialSecurityPolicyDto(decimal? EmployeeContributionRate, decimal? EmployerContributionRate,
    decimal? MinimumContributionBase, decimal? MaximumContributionBase, string? InsuredPersonClassification);
public sealed record PitTaxBracketDto(int SortOrder, decimal LowerBoundInclusive, decimal? UpperBoundExclusive, decimal Rate);
public sealed record PitPolicyDto(int? TaxYear, decimal? EmploymentExpenseDeductionRate,
    decimal? EmploymentExpenseDeductionCap, decimal? PersonalAllowanceAmount, string? WithholdingMethodIdentifier,
    IReadOnlyList<PitTaxBracketDto> Brackets);
public sealed record StatutoryPolicyDto(Guid StatutoryPolicyVersionId, Guid StatutorySchemeId, string SchemeType,
    string Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Currency, string Status,
    string? OfficialReference, string? CalculationMethodVersion, DateTime CreatedAt, DateTime UpdatedAt,
    DateTime? PublishedAt, SocialSecurityPolicyDto? SocialSecurity, PitPolicyDto? PersonalIncomeTax);
/// <summary>Resolved, NoApplicablePolicy, Ambiguous, or SchemeNotFound. No rate fallback.</summary>
public sealed record StatutoryPolicyResolution(string Outcome, string Message, StatutorySchemeDto? Scheme, StatutoryPolicyDto? Policy);

public interface IStatutoryPolicyResolver
{
    Task<StatutoryPolicyResolution> ResolveAsync(Guid schemeId, DateOnly governingDate, CancellationToken ct);
}

public interface IStatutoryPolicyService
{
    Task<IReadOnlyList<StatutorySchemeDto>> GetSchemesAsync(bool includeInactive, CancellationToken ct);
    Task<StatutorySchemeDto?> GetSchemeAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<StatutorySchemeDto>> CreateSchemeAsync(StatutorySchemeRequest request, CancellationToken ct);
    Task<ServiceResult<StatutorySchemeDto>> UpdateSchemeAsync(Guid id, StatutorySchemeRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteSchemeAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<StatutoryPolicyDto>> GetPoliciesAsync(Guid? schemeId, CancellationToken ct);
    Task<StatutoryPolicyDto?> GetPolicyAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<StatutoryPolicyDto>> CreatePolicyAsync(StatutoryPolicyCreateRequest request, CancellationToken ct);
    Task<ServiceResult<StatutoryPolicyDto>> UpdatePolicyAsync(Guid id, StatutoryPolicyRequest request, CancellationToken ct);
    Task<ServiceResult<StatutoryPolicyDto>> SetSocialSecurityAsync(Guid id, SocialSecurityPolicyRequest request, CancellationToken ct);
    Task<ServiceResult<StatutoryPolicyDto>> SetPitAsync(Guid id, PitPolicyRequest request, CancellationToken ct);
    Task<ServiceResult<StatutoryPolicyDto>> PublishAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<bool>> DeletePolicyAsync(Guid id, CancellationToken ct);
}
