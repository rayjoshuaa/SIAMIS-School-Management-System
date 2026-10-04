using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Reads, appends, and administratively removes employee history events.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/history")]
[Produces("application/json")]
public sealed class EmployeeHistoryController(IEmployeeHistoryService service) : ControllerBase
{
    /// <summary>Returns authorized history in reverse chronological order.</summary>
    /// <remarks>Employee.Read returns ordinary events; Payroll.Read returns Salary Change events. Combined capabilities return both. Unauthorized categories are omitted completely.</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeHistoryDto>>> GetHistory(Guid employeeId, CancellationToken ct)
    {
        var result = await service.GetHistoryAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeHistoryDto>>(result.Failure!);
    }

    /// <summary>Returns one authorized history event belonging to the employee.</summary>
    /// <remarks>Salary Change requires Payroll.Read; ordinary events require Employee.Read. Unauthorized or nonowned IDs return 404.</remarks>
    [HttpGet("{historyId:guid}")]
    [ProducesResponseType(typeof(EmployeeHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeHistoryDto>> GetHistoryEvent(Guid employeeId, Guid historyId, CancellationToken ct)
    {
        var result = await service.GetHistoryEventAsync(employeeId, historyId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeHistoryDto>(result.Failure!);
    }

    /// <summary>Adds an immutable employee history event. Corrections should be recorded as new events.</summary>
    /// <remarks>Salary Change requires Payroll.Manage; ordinary events require Employee.Manage. ChangedBy is descriptive; authenticated audit actor is server-derived.</remarks>
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
    /// <remarks>Salary Change requires Payroll.Manage; ordinary events require Employee.Manage. Unauthorized or nonowned IDs return 404.</remarks>
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
        "forbidden" => new(Forbid()),
        _ => new(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
