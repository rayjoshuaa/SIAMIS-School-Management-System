using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SIAMIS.Application.Employees;

[JsonConverter(typeof(WorkArrangementConverter))]
public enum ClockWorkArrangement { OnCampus, OnlineClass, RemoteWork }
public sealed class WorkArrangementConverter() : JsonStringEnumConverter<ClockWorkArrangement>(allowIntegerValues: false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeClockInRequest
{
    [Required] public Guid? RequestKey { get; set; }
    [Required] public ClockWorkArrangement? WorkArrangement { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeeClockOutRequest
{
    [Required] public Guid? RequestKey { get; set; }
    [Required] public Guid? SessionId { get; set; }
}
public sealed record EmployeeClockSessionDto(Guid SessionId, string WorkArrangement,
    Guid InEventId, DateTime ClockedInAtUtc, DateOnly InBusinessDate, Guid? OutEventId,
    DateTime? ClockedOutAtUtc, DateOnly? OutBusinessDate, string BusinessTimeZone, bool IsOpen);
public sealed record EmployeeClockResult(EmployeeClockSessionDto Session, bool IsReplay);
public interface IEmployeeClockService
{
    Task<ServiceResult<EmployeeClockResult>> ClockInAsync(EmployeeClockInRequest request, CancellationToken ct);
    Task<ServiceResult<EmployeeClockResult>> ClockOutAsync(EmployeeClockOutRequest request, CancellationToken ct);
    Task<ServiceResult<EmployeeClockSessionDto?>> CurrentAsync(CancellationToken ct);
    Task<ServiceResult<PagedResult<EmployeeClockSessionDto>>> HistoryAsync(DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
}
