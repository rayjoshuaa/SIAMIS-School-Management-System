using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages employee leave requests without leave balance or approval processing.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/leave")]
[Produces("application/json")]
public sealed class EmployeeLeaveController(IEmployeeLeaveService service) : ControllerBase
{
    /// <summary>Returns leave records overlapping the optional inclusive date range.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeLeaveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeLeaveDto>>> GetLeaves(
        Guid employeeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        var result = await service.GetLeavesAsync(employeeId, fromDate, toDate, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeLeaveDto>>(result.Failure!);
    }

    /// <summary>Returns one leave record belonging to the employee.</summary>
    [HttpGet("{leaveId:guid}")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeLeaveDto>> GetLeave(Guid employeeId, Guid leaveId, CancellationToken ct)
    {
        var result = await service.GetLeaveAsync(employeeId, leaveId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeLeaveDto>(result.Failure!);
    }

    /// <summary>Creates a leave request with inclusive calendar-day count and Pending status by default.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeLeaveDto>> CreateLeave(Guid employeeId, [FromBody] EmployeeLeaveRequest request, CancellationToken ct)
    {
        var result = await service.CreateLeaveAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeeLeaveDto>(result.Failure!);
        return CreatedAtAction(nameof(GetLeave), new { employeeId, leaveId = result.Value!.LeaveId }, result.Value);
    }

    /// <summary>Updates a leave record, recalculating inclusive days and checking for overlap.</summary>
    [HttpPut("{leaveId:guid}")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeLeaveDto>> UpdateLeave(Guid employeeId, Guid leaveId, [FromBody] EmployeeLeaveRequest request, CancellationToken ct)
    {
        var result = await service.UpdateLeaveAsync(employeeId, leaveId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeLeaveDto>(result.Failure!);
    }

    /// <summary>Deletes a leave record without changing employee or leave type data.</summary>
    [HttpDelete("{leaveId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLeave(Guid employeeId, Guid leaveId, CancellationToken ct)
    {
        var result = await service.DeleteLeaveAsync(employeeId, leaveId, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = StatusCodes.Status404NotFound })),
        "conflict" => new(StatusCode(StatusCodes.Status409Conflict, new ProblemDetails { Title = "Conflict", Detail = failure.Message, Status = StatusCodes.Status409Conflict })),
        _ => new(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
