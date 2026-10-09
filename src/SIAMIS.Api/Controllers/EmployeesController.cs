using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Produces("application/json")]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
    /// <summary>Searches and filters employees using database-side pagination.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeeListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeeListItemDto>>> GetEmployees([FromQuery] EmployeeListQuery query, CancellationToken cancellationToken)
        => Ok(await employees.GetEmployeesAsync(query, cancellationToken));

    /// <summary>Returns personal, contact, address, current employment and summary information for an employee.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto>> GetEmployee(Guid id, CancellationToken cancellationToken)
    {
        var employee = await employees.GetEmployeeAsync(id, cancellationToken);
        if (employee is null) return NotFound("Employee was not found.");
        return Ok(employee);
    }

    /// <summary>Creates an employee and their initial current employment record.</summary>
    /// <remarks>A nonempty UUID Idempotency-Key is required. Equivalent committed replays are returned for 30 days; used keys remain permanently reserved.</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDetailDto>> CreateEmployee([FromBody] CreateEmployeeRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(idempotencyKey, "D", out var requestKey) || requestKey == Guid.Empty)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A nonempty UUID Idempotency-Key is required for registration."] }) { Status = 400 });
        var result = await employees.CreateEmployeeAsync(request, requestKey, cancellationToken);
        if (!result.IsSuccess) return Failure(result.Failure!);
        return CreatedAtAction(nameof(GetEmployee), new { id = result.Value!.EmployeeId }, result.Value);
    }

    /// <summary>Corrects employee/profile and current employment data; use lifecycle endpoints for effective context changes or ending/rehiring employment. Supplied child collections replace their matching collection; omitted collections remain unchanged.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeDetailDto>> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var result = await employees.UpdateEmployeeAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }

    /// <summary>Accepts only a consistent no-op status request. Use end-employment or rehire to change lifecycle state.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] EmployeeStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await employees.SetEmployeeStatusAsync(id, request.IsActive!.Value, cancellationToken);
        return result.IsSuccess ? NoContent() : Problem(statusCode: result.Failure!.Code == "not_found" ? 404 : 409,
            title: "Employment lifecycle", detail: result.Failure.Message);
    }

    private ActionResult<EmployeeDetailDto> Failure(ApiFailure failure) => failure.Code switch
    {
        "forbidden" => new ActionResult<EmployeeDetailDto>(Forbid()),
        "idempotency_conflict" or "registration_in_progress" or "registration_key_expired" => RegistrationFailure(failure),
        "validation" => new ActionResult<EmployeeDetailDto>(BadRequest(new ValidationProblemDetails(
            new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        "not_found" => new ActionResult<EmployeeDetailDto>(NotFound(failure.Message)),
        "conflict" => new ActionResult<EmployeeDetailDto>(Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: failure.Message)),
        _ => new ActionResult<EmployeeDetailDto>(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };

    private ActionResult<EmployeeDetailDto> RegistrationFailure(ApiFailure failure)
    {
        var status = failure.Code == "registration_key_expired" ? StatusCodes.Status410Gone : StatusCodes.Status409Conflict;
        if (failure.Code == "registration_in_progress") Response.Headers.RetryAfter = "5";
        return new ActionResult<EmployeeDetailDto>(Problem(statusCode: status, title: "Employee registration", detail: failure.Message,
            extensions: new Dictionary<string, object?> { ["code"] = failure.Code }));
    }

    private ObjectResult NotFoundProblem(string message) => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: message);
}
