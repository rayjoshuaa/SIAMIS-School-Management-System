namespace SIAMIS.Domain.Entities.Payroll;

/// <summary>Authoritative generated PIT history. Source identities are retained with the exact versioned calculation snapshot.</summary>
public sealed class EmployeePayrollPitResult
{
    public Guid EmployeePayrollPitResultId { get; set; } = Guid.NewGuid();
    public Guid EmployeePayrollId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid PayrollPeriodId { get; set; }
    public DateOnly GoverningDate { get; set; }
    public int TaxYear { get; set; }
    public string Currency { get; set; } = "THB";
    public Guid StatutorySchemeId { get; set; }
    public Guid StatutoryPolicyVersionId { get; set; }
    public Guid EmployeeStatutoryEnrollmentId { get; set; }
    public Guid EmployeeTaxDeclarationId { get; set; }
    public Guid EmployeePitPaymentScheduleId { get; set; }
    public int ScheduleRevisionNumber { get; set; }
    public int ApplicablePaymentCount { get; set; }
    public int PaymentOrdinal { get; set; }
    public string CalculationMethodVersion { get; set; } = "PIT-TH-V1";
    public decimal CurrentRegularIncome { get; set; }
    public decimal PriorRecognizedIncome { get; set; }
    public decimal ProjectedRegularIncome { get; set; }
    public decimal EmploymentExpenseDeduction { get; set; }
    public decimal PersonalAllowance { get; set; }
    public decimal SpouseAllowance { get; set; }
    public decimal ChildAllowance { get; set; }
    public decimal ParentAllowance { get; set; }
    public decimal RecognizedEmployeeSso { get; set; }
    public decimal NetTaxableIncome { get; set; }
    public decimal RawAnnualTax { get; set; }
    public decimal AllocatableAnnualWithholding { get; set; }
    public decimal SubSatangRemainder { get; set; }
    public decimal PriorRecognizedWithholding { get; set; }
    public decimal CurrentWithholding { get; set; }
    public decimal? FinalAllocationResidual { get; set; }
    public bool IsFinalScheduledPayment { get; set; }
    public decimal OverWithheldAmount { get; set; }
    public string CalculationSnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
