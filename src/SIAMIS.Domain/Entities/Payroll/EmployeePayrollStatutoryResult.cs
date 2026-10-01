namespace SIAMIS.Domain.Entities.Payroll;

/// <summary>Generated historical result; no mutation API. SourceId on the deduction is not a foreign key.</summary>
public sealed class EmployeePayrollStatutoryResult
{
    public Guid EmployeePayrollStatutoryResultId { get; set; } = Guid.NewGuid();
    public Guid EmployeePayrollId { get; set; }
    public Guid StatutorySchemeId { get; set; }
    public Guid StatutoryPolicyVersionId { get; set; }
    public Guid EmployeeStatutoryEnrollmentId { get; set; }
    public string CalculationMethodVersion { get; set; } = string.Empty;
    public string ContributionMonth { get; set; } = string.Empty;
    public DateOnly GoverningDate { get; set; }
    public string Currency { get; set; } = "THB";
    public string CalculationSnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public EmployeePayrollSocialSecurityResult SocialSecurity { get; set; } = null!;
}

public sealed class EmployeePayrollSocialSecurityResult
{
    public Guid EmployeePayrollStatutoryResultId { get; set; }
    public decimal ContributionWage { get; set; }
    public decimal ContributionBase { get; set; }
    public decimal MinimumBase { get; set; }
    public decimal MaximumBase { get; set; }
    public decimal EmployeeRate { get; set; }
    public decimal EmployerRate { get; set; }
    public decimal RawEmployeeAmount { get; set; }
    public decimal EmployeeAmount { get; set; }
    public decimal EmployerAmount { get; set; }
}
