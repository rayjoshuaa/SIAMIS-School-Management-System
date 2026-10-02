using SIAMIS.Domain.Common;

namespace SIAMIS.Domain.Entities.MasterData;

public abstract class MasterDataEntity : IHasTimestamps
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Department : MasterDataEntity { }
public sealed class Designation : MasterDataEntity { }
public sealed class EmploymentType : MasterDataEntity { }
public sealed class EmploymentStatus : MasterDataEntity
{
    public bool IsTerminal { get; set; }
}
public sealed class ContractType : MasterDataEntity { }
public sealed class Location : MasterDataEntity { }
public sealed class AddressType : MasterDataEntity { }
public sealed class Country : MasterDataEntity { }
public sealed class HiringSource : MasterDataEntity { }
public sealed class Nationality : MasterDataEntity { }
public sealed class Gender : MasterDataEntity { }
public sealed class MaritalStatus : MasterDataEntity { }
public sealed class DocumentType : MasterDataEntity
{
    public bool RequiresExpiryDate { get; set; }
    public bool RequiresDocumentNumber { get; set; }
    public bool RequiresVerification { get; set; }
}
public sealed class LeaveType : MasterDataEntity
{
    public bool IsPaid { get; set; }
}
public sealed class AttendanceStatus : MasterDataEntity { }
public sealed class PayType : MasterDataEntity { }
public sealed class PayrollComponent : MasterDataEntity
{
    public string Category { get; set; } = string.Empty;
    public string CalculationMethod { get; set; } = "FixedAmount";
    public bool IsTaxable { get; set; }
    public bool IsStatutory { get; set; }
    public string? ContributionSide { get; set; }
    public string SsoWageTreatment { get; set; } = "Unknown";
    public string PitIncomeTreatment { get; set; } = "Unknown";
    /// <summary>For Percentage only: BasicSalary means the applicable salary snapshot, GrossEarnings means earnings before deductions, and GrossPay means the future engine's gross payroll amount. This is configuration only; no calculation occurs here.</summary>
    public string? PercentageBase { get; set; }
    public ICollection<SIAMIS.Domain.Entities.Payroll.EmployeePayrollLine> PayrollLines { get; set; } = new List<SIAMIS.Domain.Entities.Payroll.EmployeePayrollLine>();
}
public sealed class PerformanceRating : MasterDataEntity { }
