using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Calculated attendance coverage; reading never finalizes attendance or mutates Leave/payroll.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/attendance-days")]
[Produces("application/json")]
public sealed class AttendanceDaysController(IAttendanceDayService service) : ControllerBase
{
    /// <summary>Calculates one Bangkok business date from exact events and Approved frozen Leave. Returns structured readiness, interval provenance and truncated milliseconds with conversion residue. Conflict totals are null. No confirmed absence, overtime or monetary effects.</summary>
    /// <param name="employeeId">Owning employee, including inactive employees with historical employment.</param>
    /// <param name="date">Bangkok business date, yyyy-MM-dd.</param>
    /// <param name="ct">Cancellation.</param>
    [HttpGet("{date}")]
    [ProducesResponseType(typeof(AttendanceDayDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<AttendanceDayDto>> Get(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var r = await service.GetAsync(employeeId, date, ct);
        if (r.IsSuccess) return Ok(r.Value);
        if (r.Failure!.Code == "validation") return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["date"] = [r.Failure.Message] }) { Status = 400 });
        return r.Failure!.Code == "not_found"
            ? NotFound(new ProblemDetails { Status = 404, Title = "Not found", Detail = r.Failure.Message })
            : Conflict(new ProblemDetails { Status = 409, Title = "Retry daily read", Detail = r.Failure.Message });
    }
}
