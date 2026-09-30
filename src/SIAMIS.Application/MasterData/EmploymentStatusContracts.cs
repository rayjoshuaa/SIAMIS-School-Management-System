using System.ComponentModel.DataAnnotations;
using SIAMIS.Application.Employees;

namespace SIAMIS.Application.MasterData;

public sealed record EmploymentStatusDto(Guid Id, string? Code, string Name, string? Description, bool IsActive, bool IsTerminal);

public sealed class EmploymentStatusRequest
{
    [StringLength(50)] public string? Code { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required] public bool? IsActive { get; set; }
    [Required] public bool? IsTerminal { get; set; }
}

public interface IEmploymentStatusService
{
    Task<ServiceResult<EmploymentStatusDto>> SaveAsync(Guid? id, EmploymentStatusRequest request, CancellationToken ct);
}
