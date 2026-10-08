using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Authenticated employee-owned clocking from any location. Server UTC timestamps; no caller-selected employee or occurrence time.</summary>
[ApiController, Route("api/self/attendance"), Produces("application/json")]
public sealed class EmployeeClockController(IEmployeeClockService service) : ControllerBase
{
    /// <summary>Open one session using OnCampus, OnlineClass or RemoteWork. Retain RequestKey for safe retry.</summary>
    [HttpPost("clock-in")]
    [ProducesResponseType(typeof(EmployeeClockResult), 201)]
    [ProducesResponseType(typeof(EmployeeClockResult), 200)]
    [ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(409)]
    public async Task<IActionResult> ClockIn(EmployeeClockInRequest request, CancellationToken ct) => Result(await service.ClockInAsync(request, ct), true);
    /// <summary>Close the caller's open session. SessionId protects against a stale OUT closing a newer session. Never backdates or auto-closes evidence.</summary>
    [HttpPost("clock-out")]
    [ProducesResponseType(typeof(EmployeeClockResult), 201)]
    [ProducesResponseType(typeof(EmployeeClockResult), 200)]
    [ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<IActionResult> ClockOut(EmployeeClockOutRequest request, CancellationToken ct) => Result(await service.ClockOutAsync(request, ct), true);
    /// <summary>Own open session or null. An open session is not a validated daily attendance result.</summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(EmployeeClockSessionDto), 200)]
    [ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> Current(CancellationToken ct) => Result(await service.CurrentAsync(ct));
    /// <summary>Own sessions, paged by opening date. UTC instants must be displayed in Asia/Bangkok; closed sessions are not automatic payroll time.</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(PagedResult<EmployeeClockSessionDto>), 200)]
    [ProducesResponseType(400), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> Sessions(DateOnly? from, DateOnly? to, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Result(await service.HistoryAsync(from, to, page, pageSize, ct));
    private IActionResult Result<T>(ServiceResult<T> r, bool command = false) => r.IsSuccess
        ? command && r.Value is EmployeeClockResult { IsReplay: false } ? StatusCode(201, r.Value)
            : r.Value is null ? new JsonResult(null) { StatusCode = 200 } : Ok(r.Value)
        : r.Failure!.Code switch
        {
            "forbidden" => Forbid(),
            "not_found" => NotFound(new ProblemDetails { Status = 404, Title = "Clock session not found", Detail = r.Failure.Message }),
            "conflict" => Conflict(new ProblemDetails { Status = 409, Title = "Clock state changed", Detail = r.Failure.Message }),
            _ => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [r.Failure.Message] }) { Status = 400 })
        };
}
