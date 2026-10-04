using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Immutable attendance observations and expected schedules. Ownership checks are not caller authorization.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}")]
[Produces("application/json")]
public sealed class AttendanceFoundationController(IAttendanceFoundationService service, IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Development-only manual evidence. Requires explicit-offset timestamp, reason and global request key. Source/actor are server-controlled. Replay returns 200; new evidence 201. No pairing or daily calculation.</summary>
    [HttpPost("attendance-events/manual")]
    [ProducesResponseType(typeof(AttendanceEventDto), 201)]
    [ProducesResponseType(typeof(AttendanceEventDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<AttendanceEventDto>> Manual(Guid employeeId, ManualAttendanceEventRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var result = await service.CreateManualAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<AttendanceEventDto>(result.Failure!);
        return result.Value!.IsReplay ? Ok(result.Value.Event) : CreatedAtAction(nameof(Event), new { employeeId, eventId = result.Value.Event.AttendanceEventId }, result.Value.Event);
    }
    /// <summary>Reads immutable evidence for its owning employee. No PUT or DELETE is supported.</summary>
    [HttpGet("attendance-events/{eventId:guid}")]
    [ProducesResponseType(typeof(AttendanceEventDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<AttendanceEventDto>> Event(Guid employeeId, Guid eventId, CancellationToken ct)
    {
        var r = await service.EventAsync(employeeId, eventId, ct); return r.IsSuccess ? Ok(r.Value) : Failure<AttendanceEventDto>(r.Failure!);
    }
    /// <summary>Pages event evidence ordered by Bangkok business date, UTC instant and ID. Inclusive date filters; pageSize 1–100.</summary>
    /// <param name="employeeId">Owning employee.</param><param name="fromDate">Optional inclusive Bangkok date.</param><param name="toDate">Optional inclusive Bangkok date.</param><param name="page">One-based page.</param><param name="pageSize">Page size, 1–100.</param><param name="ct">Cancellation.</param>
    [HttpGet("attendance-events")]
    [ProducesResponseType(typeof(PagedResult<AttendanceEventDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<PagedResult<AttendanceEventDto>>> Events(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var r = await service.EventsAsync(employeeId, fromDate, toDate, page, pageSize, ct); return r.IsSuccess ? Ok(r.Value) : Failure<PagedResult<AttendanceEventDto>>(r.Failure!);
    }
    /// <summary>Resolves date-effective employment and explicit calendar in Asia/Bangkok. 200 includes readiness; missing configuration is not zero scheduled work. Creates no daily result and requires no LeavePolicy.</summary>
    /// <param name="employeeId">Owning employee.</param><param name="date">Required Bangkok business date.</param><param name="ct">Cancellation.</param>
    [HttpGet("attendance-expected-work")]
    [ProducesResponseType(typeof(AttendanceExpectedWorkDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<AttendanceExpectedWorkDto>> ExpectedWork(Guid employeeId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        if (!date.HasValue) return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["date"] = ["date is required."] }) { Status = 400 });
        var r = await service.ExpectedWorkAsync(employeeId, date.Value, ct); return r.IsSuccess ? Ok(r.Value) : Failure<AttendanceExpectedWorkDto>(r.Failure!);
    }
    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] }) { Status = 400 })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = 404 })),
        "conflict" => new(StatusCode(409, new ProblemDetails { Title = "Conflict", Detail = failure.Message, Status = 409 })),
        _ => new(Problem(statusCode: 500))
    };
}
