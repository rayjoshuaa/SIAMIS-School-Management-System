using SIAMIS.Domain.Common;
using SIAMIS.Domain.Entities.Employees;

namespace SIAMIS.Domain.Entities.Payroll;

public sealed class EmployeeStatutoryEnrollment : IHasTimestamps
{
    public Guid EmployeeStatutoryEnrollmentId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid StatutorySchemeId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Applicability { get; set; } = string.Empty;
    public string? MembershipNumber { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Employee Employee { get; set; } = null!;
    public StatutoryScheme Scheme { get; set; } = null!;
}

public sealed class EmployeeTaxProfile : IHasTimestamps
{
    public Guid EmployeeTaxProfileId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public string? TaxpayerIdentificationNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Employee Employee { get; set; } = null!;
}

public sealed class EmployeeTaxDeclaration : IHasTimestamps
{
    public Guid EmployeeTaxDeclarationId { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public int TaxYear { get; set; }
    // Year-specific treatment shares the declaration's immutable revision/verification lifecycle.
    public string ResidencyStatus { get; set; } = "Unknown";
    public string EmploymentTaxTreatment { get; set; } = "Unknown";
    public int RevisionNumber { get; set; }
    public Guid? ReplacesDeclarationId { get; set; }
    public string Status { get; set; } = "Draft";
    // Captured by verification, never obtained from a changed live profile when reading history.
    public string? TaxpayerIdentificationNumberSnapshot { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Employee Employee { get; set; } = null!;
    public EmployeeTaxDeclaration? ReplacesDeclaration { get; set; }
    public ICollection<EmployeeTaxClaim> Claims { get; set; } = new List<EmployeeTaxClaim>();
    public EmployeeTaxOpeningBalance? OpeningBalance { get; set; }
}

// Operational selection is separate from immutable Verified declaration inputs.
public sealed class EmployeeTaxDeclarationSelection
{
    public Guid EmployeeId { get; set; }
    public int TaxYear { get; set; }
    public Guid CurrentDeclarationId { get; set; }
    public EmployeeTaxDeclaration Declaration { get; set; } = null!;
}

public sealed class EmployeeTaxClaim : IHasTimestamps
{
    public Guid EmployeeTaxClaimId { get; set; } = Guid.NewGuid();
    public Guid EmployeeTaxDeclarationId { get; set; }
    public string ClaimType { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public int? Quantity { get; set; }
    public string? Reference { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public EmployeeTaxDeclaration Declaration { get; set; } = null!;
}

public sealed class EmployeeTaxOpeningBalance : IHasTimestamps
{
    public Guid EmployeeTaxDeclarationId { get; set; }
    public string State { get; set; } = string.Empty;
    public string Currency { get; set; } = "THB";
    // Aggregate pre-SIAMIS taxable Thai employment income through inclusive AsOfDate.
    // This is neither GrossPay nor automatically generic TaxableEarnings; D6 interprets it.
    public decimal? PriorTaxableEmploymentIncome { get; set; }
    public decimal? PriorTaxWithheld { get; set; }
    public decimal? PriorSocialSecurityContribution { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string? Remarks { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public EmployeeTaxDeclaration Declaration { get; set; } = null!;
}
