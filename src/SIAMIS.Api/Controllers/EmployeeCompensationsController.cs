using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages compensation history for an employee.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/compensations")]
[Produces("application/json")]
public sealed class EmployeeCompensationsController(IEmployeeCompensationService service) : ControllerBase
{
    /// <summary>Returns compensation history, newest effective date first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeCompensationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeCompensationDto>>> GetCompensations(Guid employeeId, CancellationToken ct)
    {
        var result = await service.GetCompensationsAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeCompensationDto>>(result.Failure!);
    }

    /// <summary>Returns one compensation record for the specified employee.</summary>
    [HttpGet("{compensationId:guid}")]
    [ProducesResponseType(typeof(EmployeeCompensationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeCompensationDto>> GetCompensation(Guid employeeId, Guid compensationId, CancellationToken ct)
    {
        var result = await service.GetCompensationAsync(employeeId, compensationId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeCompensationDto>(result.Failure!);
    }

    /// <summary>Creates a compensation record and, when current, closes the prior current record.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeCompensationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeCompensationDto>> CreateCompensation(Guid employeeId, [FromBody] EmployeeCompensationRequest request, CancellationToken ct)
    {
        var result = await service.CreateCompensationAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeeCompensationDto>(result.Failure!);
        return CreatedAtAction(nameof(GetCompensation), new { employeeId, compensationId = result.Value!.CompensationId }, result.Value);
    }

    /// <summary>Updates one compensation record without changing its employee ownership.</summary>
    [HttpPut("{compensationId:guid}")]
    [ProducesResponseType(typeof(EmployeeCompensationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeCompensationDto>> UpdateCompensation(Guid employeeId, Guid compensationId, [FromBody] EmployeeCompensationRequest request, CancellationToken ct)
    {
        var result = await service.UpdateCompensationAsync(employeeId, compensationId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeCompensationDto>(result.Failure!);
    }

    /// <summary>Deletes a compensation record. Deleting the current record does not promote another record.</summary>
    [HttpDelete("{compensationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCompensation(Guid employeeId, Guid compensationId, CancellationToken ct)
    {
        var result = await service.DeleteCompensationAsync(employeeId, compensationId, ct);
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
