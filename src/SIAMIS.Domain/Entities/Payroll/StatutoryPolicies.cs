using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class StatutoryScheme : IHasTimestamps
{
    public Guid StatutorySchemeId { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Jurisdiction { get; set; } = "TH";
    public string SchemeType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class StatutoryPolicyVersion : IHasTimestamps
{
    public Guid StatutoryPolicyVersionId { get; set; } = Guid.NewGuid();
    public Guid StatutorySchemeId { get; set; }
    public string SchemeType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = "Draft";
    public string? OfficialReference { get; set; }
    public string? CalculationMethodVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public StatutoryScheme StatutoryScheme { get; set; } = null!;
    public SocialSecurityPolicyConfiguration? SocialSecurity { get; set; }
    public PitPolicyConfiguration? PersonalIncomeTax { get; set; }
}

public sealed class SocialSecurityPolicyConfiguration
{
    public Guid StatutoryPolicyVersionId { get; set; }
    public string SchemeType { get; set; } = "SocialSecurity";
    // Rates are percentage points, consistent with existing PayrollRule.Rate.
    public decimal? EmployeeContributionRate { get; set; }
    public decimal? EmployerContributionRate { get; set; }
    public decimal? MinimumContributionBase { get; set; }
    public decimal? MaximumContributionBase { get; set; }
    public string? InsuredPersonClassification { get; set; }
    public StatutoryPolicyVersion Policy { get; set; } = null!;
}

public sealed class PitPolicyConfiguration
{
    public Guid StatutoryPolicyVersionId { get; set; }
    public string SchemeType { get; set; } = "PersonalIncomeTax";
    public int? TaxYear { get; set; }
    public decimal? EmploymentExpenseDeductionRate { get; set; }
    public decimal? EmploymentExpenseDeductionCap { get; set; }
    public decimal? PersonalAllowanceAmount { get; set; }
    public decimal? SpouseAllowanceAmount { get; set; }
    public decimal? ChildAllowanceAmount { get; set; }
    public decimal? AdditionalChildAllowanceAmount { get; set; }
    public decimal? ParentAllowanceAmount { get; set; }
    public int? AdoptedChildCombinedCountLimit { get; set; }
    public int? MaximumEligibleParentCount { get; set; }
    // Audit identifier only; D4A does not assign annualization/cumulative semantics.
    public string? WithholdingMethodIdentifier { get; set; }
    public StatutoryPolicyVersion Policy { get; set; } = null!;
    public ICollection<PitTaxBracket> Brackets { get; set; } = new List<PitTaxBracket>();
}

public sealed class PitTaxBracket
{
    public Guid PitTaxBracketId { get; set; } = Guid.NewGuid();
    public Guid StatutoryPolicyVersionId { get; set; }
    public int SortOrder { get; set; }
    public decimal LowerBoundInclusive { get; set; }
    public decimal? UpperBoundExclusive { get; set; }
    public decimal Rate { get; set; }
    public PitPolicyConfiguration Configuration { get; set; } = null!;
}
