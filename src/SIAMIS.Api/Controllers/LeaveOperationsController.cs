using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Leave history, Pending review queue and derived calendar-year balances.</summary>
[ApiController]
[Produces("application/json")]
public sealed class LeaveOperationsController(IEmployeeLeaveService service) : ControllerBase
{
    /// <summary>Paged leave history, filtered by employee, type, inclusive date overlap and status; newest request first.</summary>
    [HttpGet("api/leave-requests")]
    [ProducesResponseType(typeof(PagedResult<LeaveHistoryItemDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<PagedResult<LeaveHistoryItemDto>>> History([FromQuery] LeaveHistoryQuery query, CancellationToken ct)
    {
        var result = await service.HistoryAsync(query, false, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PagedResult<LeaveHistoryItemDto>>(result.Failure!);
    }
    /// <summary>Paged Pending-only approval queue with employee identity, notice and document-requirement metadata.</summary>
    [HttpGet("api/leave-requests/pending")]
    [ProducesResponseType(typeof(PagedResult<LeaveHistoryItemDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<PagedResult<LeaveHistoryItemDto>>> Pending([FromQuery] LeaveHistoryQuery query, CancellationToken ct)
    {
        var result = await service.HistoryAsync(query, true, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PagedResult<LeaveHistoryItemDto>>(result.Failure!);
    }
    /// <summary>Calendar-year balances derived from entitlements, append-only adjustments and frozen Pending/Approved allocations. Null entitlement differs from configured zero.</summary>
    /// <param name="employeeId">Employee identity.</param>
    /// <param name="leaveYear">Calendar year 1–9999. Required.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet("api/employees/{employeeId:guid}/leave-balances")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveBalanceDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceDto>>> Balances(Guid employeeId, [FromQuery] int leaveYear, CancellationToken ct)
    {
        var result = await service.BalancesAsync(employeeId, leaveYear, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<LeaveBalanceDto>>(result.Failure!);
    }
    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] }) { Status = 400 })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = 404 })),
        "conflict" => new(StatusCode(409, new ProblemDetails { Title = "Conflict", Detail = failure.Message, Status = 409 })),
        _ => new(Problem(statusCode: 500))
    };
}
