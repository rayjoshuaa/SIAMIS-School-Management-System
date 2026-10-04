using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Authoritative scheduled-minute leave requests and controlled lifecycle commands.</summary>
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

    /// <summary>Calculates FullDay/Timed scheduled minutes and freezes V2 paid/unpaid intervals. Paid tracked leave reserves only available entitlement; excess is unpaid. Missing entitlement remains a conflict. Always creates Pending.</summary>
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

    /// <summary>Approves Pending leave using its frozen calculation and existing reservation, without recalculation.</summary>
    [HttpPost("{leaveId:guid}/approve")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeLeaveDto>> Approve(Guid employeeId, Guid leaveId, [FromBody] LeaveReviewRequest request, CancellationToken ct)
    {
        var result = await service.ApproveAsync(employeeId, leaveId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeLeaveDto>(result.Failure!);
    }

    /// <summary>Rejects Pending leave, releases its reservation and preserves its evidence.</summary>
    [HttpPost("{leaveId:guid}/reject")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeLeaveDto>> Reject(Guid employeeId, Guid leaveId, [FromBody] LeaveReviewRequest request, CancellationToken ct)
    {
        var result = await service.RejectAsync(employeeId, leaveId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeLeaveDto>(result.Failure!);
    }

    /// <summary>Cancels only when locked status matches the required ExpectedStatus (Pending/Approved); stale state returns 409. Approved cancellation requires remarks.</summary>
    [HttpPost("{leaveId:guid}/cancel")]
    [ProducesResponseType(typeof(EmployeeLeaveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeLeaveDto>> Cancel(Guid employeeId, Guid leaveId, [FromBody] LeaveCancellationRequest request, CancellationToken ct)
    {
        var result = await service.CancelAsync(employeeId, leaveId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeLeaveDto>(result.Failure!);
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
