using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages manually recorded attendance for employees.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/attendance")]
[Produces("application/json")]
public sealed class EmployeeAttendanceController(IEmployeeAttendanceService service) : ControllerBase
{
    /// <summary>Returns attendance records, optionally filtered by inclusive calendar-date bounds.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeAttendanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeAttendanceDto>>> GetAttendance(
        Guid employeeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        var result = await service.GetAttendanceAsync(employeeId, fromDate, toDate, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeAttendanceDto>>(result.Failure!);
    }

    /// <summary>Returns one attendance record belonging to the employee.</summary>
    [HttpGet("{attendanceId:guid}")]
    [ProducesResponseType(typeof(EmployeeAttendanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeAttendanceDto>> GetAttendanceRecord(Guid employeeId, Guid attendanceId, CancellationToken ct)
    {
        var result = await service.GetAttendanceRecordAsync(employeeId, attendanceId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeAttendanceDto>(result.Failure!);
    }

    /// <summary>Retired legacy write. Valid authorized legacy requests return 410 Gone; use authorized manual attendance events. Legacy storage is retained.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeAttendanceDto>> CreateAttendance(Guid employeeId, [FromBody] EmployeeAttendanceRequest request, CancellationToken ct)
    {
        var result = await service.CreateAttendanceAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeeAttendanceDto>(result.Failure!);
        return CreatedAtAction(nameof(GetAttendanceRecord), new { employeeId, attendanceId = result.Value!.AttendanceId }, result.Value);
    }

    /// <summary>Retired legacy write. Returns 410 Gone; observations cannot be rewritten.</summary>
    [HttpPut("{attendanceId:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeAttendanceDto>> UpdateAttendance(Guid employeeId, Guid attendanceId, [FromBody] EmployeeAttendanceRequest request, CancellationToken ct)
    {
        var result = await service.UpdateAttendanceAsync(employeeId, attendanceId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeAttendanceDto>(result.Failure!);
    }

    /// <summary>Retired legacy write. Returns 410 Gone; legacy storage and reads are retained.</summary>
    [HttpDelete("{attendanceId:guid}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> DeleteAttendance(Guid employeeId, Guid attendanceId, CancellationToken ct)
    {
        var result = await service.DeleteAttendanceAsync(employeeId, attendanceId, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "retired" => new(StatusCode(StatusCodes.Status410Gone, new ProblemDetails { Title = "Legacy attendance writes retired", Detail = failure.Message, Status = StatusCodes.Status410Gone })),
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = StatusCodes.Status404NotFound })),
        "conflict" => new(StatusCode(StatusCodes.Status409Conflict, new ProblemDetails { Title = "Conflict", Detail = failure.Message, Status = StatusCodes.Status409Conflict })),
        _ => new(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
