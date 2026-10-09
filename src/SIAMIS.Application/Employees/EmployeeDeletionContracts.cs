using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Employees;

public sealed record EmployeeDeletionAssessment(string State, string Version, IReadOnlyList<string> Blockers);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeDeletionRequest
{
    [Required, StringLength(30)] public string EmployeeNumber { get; set; } = string.Empty;
    [Required, StringLength(120, MinimumLength = 5)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(64, MinimumLength = 64)] public string Version { get; set; } = string.Empty;
}
public interface IEmployeeDeletionService
{
    Task<ServiceResult<EmployeeDeletionAssessment>> AssessAsync(Guid id, CancellationToken ct);
    Task<ServiceResult<bool>> DeleteAsync(Guid id, EmployeeDeletionRequest request, CancellationToken ct);
    Task AuditDeniedAsync(Guid id, CancellationToken ct);
    Task AuditAttemptAsync(Guid id, CancellationToken ct);
}
