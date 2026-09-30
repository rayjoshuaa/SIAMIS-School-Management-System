using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Reads, appends, and administratively removes employee history events.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/history")]
[Produces("application/json")]
public sealed class EmployeeHistoryController(IEmployeeHistoryService service) : ControllerBase
{
    /// <summary>Returns an employee's history in reverse chronological order.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeHistoryDto>>> GetHistory(Guid employeeId, CancellationToken ct)
    {
        var result = await service.GetHistoryAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeHistoryDto>>(result.Failure!);
    }

    /// <summary>Returns one history event belonging to the employee.</summary>
    [HttpGet("{historyId:guid}")]
    [ProducesResponseType(typeof(EmployeeHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeHistoryDto>> GetHistoryEvent(Guid employeeId, Guid historyId, CancellationToken ct)
    {
        var result = await service.GetHistoryEventAsync(employeeId, historyId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeHistoryDto>(result.Failure!);
    }

    /// <summary>Adds an immutable employee history event. Corrections should be recorded as new events.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeHistoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeHistoryDto>> CreateHistoryEvent(Guid employeeId, [FromBody] CreateEmployeeHistoryRequest request, CancellationToken ct)
    {
        var result = await service.CreateHistoryEventAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeeHistoryDto>(result.Failure!);
        return CreatedAtAction(nameof(GetHistoryEvent), new { employeeId, historyId = result.Value!.EmployeeHistoryId }, result.Value);
    }

    /// <summary>Deletes a history event for controlled administrative correction.</summary>
    [HttpDelete("{historyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHistoryEvent(Guid employeeId, Guid historyId, CancellationToken ct)
    {
        var result = await service.DeleteHistoryEventAsync(employeeId, historyId, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = StatusCodes.Status404NotFound })),
        _ => new(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
