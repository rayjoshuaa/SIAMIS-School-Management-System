using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

public sealed record PayrollComponentAssignmentComponentDto(Guid Id, string? Code, string Name, string ComponentType,
    string CalculationMethod, string? PercentageBase, bool IsActive, bool IsTaxable, bool IsStatutory, string? ContributionSide);

public sealed record EmployeePayrollComponentAssignmentDto(
    Guid EmployeePayrollComponentAssignmentId,
    Guid EmployeeId,
    Guid PayrollComponentId,
    PayrollComponentAssignmentComponentDto PayrollComponent,
    decimal Amount,
    decimal? Quantity,
    decimal? Rate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Remarks,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record EmployeePayrollComponentAssignmentListItemDto(
    Guid EmployeePayrollComponentAssignmentId,
    Guid EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    Guid PayrollComponentId,
    string? ComponentCode,
    string ComponentName,
    string ComponentType,
    string CalculationMethod,
    string? PercentageBase,
    bool IsTaxable,
    bool IsStatutory,
    string? ContributionSide,
    decimal Amount,
    decimal? Quantity,
    decimal? Rate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Remarks,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class EmployeePayrollComponentAssignmentRequest
{
    [Required] public Guid? PayrollComponentId { get; set; }
    [Required, Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? Amount { get; set; }
    [Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? Quantity { get; set; }
    [Range(typeof(decimal), "0", "999999999999999.9999")] public decimal? Rate { get; set; }
    [Required] public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    [StringLength(1000)] public string? Remarks { get; set; }
}

public sealed class EmployeePayrollComponentAssignmentListQuery
{
    public Guid? EmployeeId { get; set; }
    public Guid? PayrollComponentId { get; set; }
    public DateOnly? ActiveOn { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public interface IEmployeePayrollComponentAssignmentService
{
    Task<ServiceResult<IReadOnlyList<EmployeePayrollComponentAssignmentDto>>> GetEmployeeAssignmentsAsync(Guid employeeId, Guid? payrollComponentId, DateOnly? activeOn, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollComponentAssignmentDto>> GetEmployeeAssignmentAsync(Guid employeeId, Guid assignmentId, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollComponentAssignmentDto>> CreateEmployeeAssignmentAsync(Guid employeeId, EmployeePayrollComponentAssignmentRequest request, CancellationToken ct);
    Task<ServiceResult<EmployeePayrollComponentAssignmentDto>> UpdateEmployeeAssignmentAsync(Guid employeeId, Guid assignmentId, EmployeePayrollComponentAssignmentRequest request, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteEmployeeAssignmentAsync(Guid employeeId, Guid assignmentId, CancellationToken ct);
    Task<ServiceResult<PagedResult<EmployeePayrollComponentAssignmentListItemDto>>> GetAssignmentsAsync(EmployeePayrollComponentAssignmentListQuery query, CancellationToken ct);
}
