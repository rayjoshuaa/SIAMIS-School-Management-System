using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Security;

public interface IResourceAccessService
{
    Task<bool> OwnFinalPayrollAsync(Guid payrollId, Guid employeeId, CancellationToken ct);
}
public sealed record StaffOverviewDto(Guid EmployeeId, string EmployeeNumber, string FirstName, string LastName, bool IsActive);
public sealed record LeaveStatusOverviewDto(Guid LeaveId, Guid EmployeeId, DateOnly StartDate, DateOnly EndDate, string Status, int? ChargeableMinutes);
public interface IHrSecurityReadService
{
    Task<SIAMIS.Application.Employees.PagedResult<StaffOverviewDto>> StaffAsync(int page, int size, CancellationToken ct);
    Task<SIAMIS.Application.Employees.PagedResult<LeaveStatusOverviewDto>> LeaveStatusAsync(int page, int size, CancellationToken ct);
    Task<StaffOverviewDto?> ProfileAsync(Guid employeeId, CancellationToken ct);
    Task<SIAMIS.Application.Employees.PagedResult<SIAMIS.Application.Payroll.EmployeePayrollListItemDto>> OwnPayrollsAsync(Guid employeeId, int page, int size, CancellationToken ct);
}

public interface IAccountService
{
    Task<bool> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<SecurityUserDto?> MeAsync();
    Task<SIAMIS.Application.Employees.ServiceResult<SecurityUserDto>> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<SIAMIS.Application.Employees.ServiceResult<SecurityUserDto>> StatusAsync(Guid id, UserStatusRequest request, CancellationToken ct);
    Task<SIAMIS.Application.Employees.ServiceResult<SecurityUserDto>> RolesAsync(Guid id, UserRolesRequest request, CancellationToken ct);
    Task<SIAMIS.Application.Employees.ServiceResult<SecurityUserDto>> GetAsync(Guid id);
    Task<SIAMIS.Application.Employees.PagedResult<SecurityUserDto>> ListAsync(int page, int size, CancellationToken ct);
    Task<bool> ChangePasswordAsync(ChangePasswordRequest request);
}

public interface ICurrentActor
{
    Guid? UserId { get; }
    Guid? EmployeeId { get; }
    string Operation { get; }
    bool HasCapability(string capability);
}

public static class SecurityCapabilities
{
    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["SystemAdmin"] = ["Security.Manage", "Employee.Read", "Employee.Manage", "Leave.Read", "Leave.Manage", "Leave.Review", "Leave.Evidence", "Attendance.Read", "Attendance.Manage", "Attendance.Finalize", "Payroll.Read", "Payroll.Manage", "Reporting.Read"],
        ["HRAdmin"] = ["HRDocuments.Read", "HRDocuments.Manage", "Employee.Read", "Employee.Manage", "Leave.Read", "Leave.Manage", "Leave.Review", "Leave.Evidence", "Attendance.Read", "Attendance.Manage", "Attendance.Finalize", "Reporting.Read"],
        ["PayrollAdmin"] = ["Payroll.Read", "Payroll.Manage"],
        ["Management"] = ["Reporting.Read"],
        ["Employee"] = ["SelfService"]
    };
    public static string[] ForRoles(IEnumerable<string> roles) => roles.Where(Roles.ContainsKey).SelectMany(r => Roles[r].Append("MasterData.Read")).Distinct().Order().ToArray();
}

public sealed record SecurityUserDto(Guid UserId, string UserName, string? Email, Guid? EmployeeId, bool IsActive, bool RequiresPasswordChange, string Version, IReadOnlyList<string> Roles, IReadOnlyList<string> Capabilities)
{
    public Guid? CurrentEmploymentRecordId { get; init; }
    public string? CurrentEmploymentStatus { get; init; }
    public bool? HasCurrentEmployment { get; init; }
    public bool RequiresOffboardingDecision { get; init; }
    public bool CredentialEstablished { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool IsLockedOut { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LoginRequest
{
    [Required, StringLength(256)] public string UserName { get; set; } = string.Empty;
    [Required, StringLength(256)] public string Password { get; set; } = string.Empty;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateUserRequest
{
    [Required, StringLength(256)] public string UserName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string? Email { get; set; }
    public Guid? EmployeeId { get; set; }
    [Required] public string[] Roles { get; set; } = [];
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UserStatusRequest
{
    [Required] public bool? IsActive { get; set; }
    [Required] public string Version { get; set; } = string.Empty;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UserRolesRequest
{
    [Required] public string[] Roles { get; set; } = [];
    [Required] public string Version { get; set; } = string.Empty;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChangePasswordRequest
{
    [Required, StringLength(256)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, StringLength(256)] public string NewPassword { get; set; } = string.Empty;
}
