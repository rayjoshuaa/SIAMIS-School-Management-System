using SIAMIS.Application.Employees;

namespace SIAMIS.Application.Payroll;

/// <summary>Employment context at the first eligible point in a payroll period, independent of IsCurrent and Employee.IsActive.</summary>
public sealed record PayrollEmploymentContextDto(Guid EmploymentRecordId, DateOnly TargetContextDate,
    Guid? DepartmentId, string? DepartmentName, Guid? DesignationId, string? DesignationName,
    Guid? EmploymentTypeId, string? EmploymentTypeName, Guid? LocationId, string? LocationName);

public interface IPayrollEmploymentContextService
{
    /// <summary>Null context means no employment overlap; failure means ambiguous/corrupt history.</summary>
    Task<IReadOnlyDictionary<Guid, ServiceResult<PayrollEmploymentContextDto?>>> ResolveAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct);
}
