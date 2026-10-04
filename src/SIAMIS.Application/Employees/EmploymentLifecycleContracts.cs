using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Employees;

/// <summary>Omitted context IDs retain the current value. Use Employee PUT for corrections.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmploymentChangeRequest
{
    [Required] public DateOnly? EffectiveDate { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? DesignationId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? EmploymentTypeId { get; set; }
    public Guid? EmploymentStatusId { get; set; }
    public Guid? ReportingToEmployeeId { get; set; }
    public Guid? HiringSourceId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EndEmploymentRequest
{
    [Required] public Guid? ExpectedEmploymentRecordId { get; set; }
    [Required] public DateOnly? EndDate { get; set; }
    public bool? DisableLinkedAccount { get; set; }
    [StringLength(36)] public string? ExpectedLinkedAccountVersion { get; set; }
    [Required] public Guid? EmploymentStatusId { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RehireRequest
{
    [Required] public DateOnly? HireDate { get; set; }
    public DateOnly? StartDate { get; set; }
    [Required] public Guid? DepartmentId { get; set; }
    [Required] public Guid? DesignationId { get; set; }
    public Guid? LocationId { get; set; }
    [Required] public Guid? EmploymentTypeId { get; set; }
    [Required] public Guid? EmploymentStatusId { get; set; }
    public Guid? ReportingToEmployeeId { get; set; }
    public Guid? HiringSourceId { get; set; }
}

public sealed record EmploymentRecordDto(Guid EmploymentRecordId, Guid EmployeeId, Guid? DepartmentId,
    Guid? DesignationId, Guid? LocationId, Guid? EmploymentTypeId, Guid? EmploymentStatusId,
    Guid? ReportingToEmployeeId, Guid? HiringSourceId, DateOnly HireDate, DateOnly? StartDate,
    DateOnly? EndDate, bool IsCurrent);

public interface IEmploymentLifecycleService
{
    Task<ServiceResult<EmploymentRecordDto>> ChangeAsync(Guid employeeId, EmploymentChangeRequest request, CancellationToken ct);
    Task<ServiceResult<EmploymentRecordDto>> EndAsync(Guid employeeId, EndEmploymentRequest request, CancellationToken ct);
    Task<ServiceResult<EmploymentRecordDto>> RehireAsync(Guid employeeId, RehireRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<EmploymentRecordDto>>> HistoryAsync(Guid employeeId, CancellationToken ct);
}

/// <summary>Resolves date-effective context without using IsCurrent or changing payroll selection.</summary>
public interface IEmploymentResolver
{
    Task<ServiceResult<EmploymentRecordDto?>> ResolveAsync(Guid employeeId, DateOnly date, CancellationToken ct);
}

/// <summary>Safe HR account readiness; the version is an administration concurrency token, not a security stamp.</summary>
public sealed record EmployeeAccountLifecycleDto(Guid EmployeeId, bool AccountLinked, Guid? LinkedUserId, string? AccountStatus,
    string? LinkedAccountVersion, Guid? CurrentEmploymentRecordId, string? CurrentEmploymentStatus, bool HasCurrentEmployment,
    bool RequiresOffboardingDecision);
public interface IEmployeeAccountLifecycleService
{
    Task<ServiceResult<EmployeeAccountLifecycleDto>> GetAsync(Guid employeeId, CancellationToken ct);
    // Caller owns the Employee-first Serializable transaction; no independent commit.
    Task<ApiFailure?> ResolveEndAsync(Guid employeeId, Guid employmentRecordId, EndEmploymentRequest request, CancellationToken ct);
}
