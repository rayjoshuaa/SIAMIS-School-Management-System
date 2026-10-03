using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed record OrganizationProfileDto(Guid OrganizationProfileId, string DisplayName,
    string? AddressLine1, string? AddressLine2, string? Phone, string? Email, DateTime CreatedAt, DateTime UpdatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class OrganizationProfileRequest
{
    [Required, StringLength(200)] public string DisplayName { get; set; } = string.Empty;
    [StringLength(300)] public string? AddressLine1 { get; set; }
    [StringLength(300)] public string? AddressLine2 { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
}

public sealed record PayslipEmployer(Guid OrganizationProfileId, string DisplayName, string? AddressLine1, string? AddressLine2);
public sealed record PayslipEmployee(Guid EmployeeId, string EmployeeCode, string DisplayName);
public sealed record PayslipPeriod(Guid PayrollPeriodId, string Code, string Name, DateOnly StartDate, DateOnly EndDate, DateOnly PayDate);
public sealed record PayslipLine(Guid EmployeePayrollLineId, Guid PayrollComponentId, string ComponentCode,
    string ComponentName, string ComponentType, decimal Amount, string SourceType, Guid? SourceId, string? Remarks);
public sealed record PayslipStatutorySummary(decimal? EmployeeSso, decimal? EmployerSso, decimal? PitWithholding);
public sealed record PayslipTotals(decimal BasicSalary, decimal GrossPay, decimal TaxableEarnings, decimal TotalDeductions, decimal NetPay);
public sealed record PayslipSnapshot(int Version, Guid EmployeePayrollId, DateTime CreatedAt,
    PayslipEmployer Employer, PayslipEmployee Employee, PayrollEmploymentContextDto EmploymentContext,
    PayslipPeriod Period, string Currency, IReadOnlyList<PayslipLine> Earnings, IReadOnlyList<PayslipLine> Deductions,
    PayslipStatutorySummary Statutory, PayslipTotals Totals);
public sealed record PayslipDto(Guid EmployeePayslipId, string PayrollStatus, DateTime? ApprovedAt, DateTime? PaidAt,
    DateTime? CancelledAt, int SnapshotVersion, DateTime CreatedAt, DateTime UpdatedAt, PayslipSnapshot Snapshot);
public sealed record PayrollReviewFinding(string Code, string Message);
public sealed record PayrollReviewDto(Guid EmployeePayrollId, string Status, bool CanApprove, bool PayslipReady,
    IReadOnlyList<PayrollReviewFinding> Findings);
public sealed record PayrollOperationalDto(bool HasFrozenPresentation, PayslipSnapshot? Snapshot, string? Currency,
    PayslipStatutorySummary Statutory, IReadOnlyList<PayslipLine> Earnings, IReadOnlyList<PayslipLine> Deductions, PayrollReviewDto Review);
public sealed record PayrollStatusCount(string Status, int Count);
public sealed record PayrollCurrencySummary(string Currency, int EmployeeCount, decimal BasicSalaryTotal,
    decimal GrossPayTotal, decimal TaxableEarningsTotal, decimal TotalDeductions, decimal NetPayTotal,
    decimal EmployeeSsoTotal, decimal EmployerSsoTotal, decimal PitWithholdingTotal);
public sealed record PayrollPeriodOperationsDto(EmployeePayrollPeriodSummaryDto PayrollPeriod, int PayrollCount,
    int EmployeeCount, IReadOnlyList<PayrollStatusCount> StatusCounts, IReadOnlyList<PayrollCurrencySummary> CurrencySummaries,
    int UnresolvedCurrencyCount);

public interface IOrganizationProfileService
{
    Task<OrganizationProfileDto?> GetAsync(CancellationToken ct);
    Task<OrganizationProfileDto> PutAsync(OrganizationProfileRequest request, CancellationToken ct);
}
public interface IPayrollOperationsService
{
    Task<ServiceResult<PayslipDto>> GetPayslipAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<PayrollReviewDto>> ReviewAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<PayrollPeriodOperationsDto>> GetPeriodSummaryAsync(Guid id, CancellationToken ct);
}

public static class PayrollDisplayName
{
    public static string Format(string? preferred, string? first, string? middle, string? last)
        => string.Join(' ', new[] { string.IsNullOrWhiteSpace(preferred) ? first : preferred, middle, last }
            .SelectMany(x => (x ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
}
