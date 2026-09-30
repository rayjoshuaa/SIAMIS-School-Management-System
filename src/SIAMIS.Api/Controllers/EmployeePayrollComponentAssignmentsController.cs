using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages effective-dated payroll component assignments for an employee.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/payroll-component-assignments")]
[Produces("application/json")]
public sealed class EmployeePayrollComponentAssignmentsController(IEmployeePayrollComponentAssignmentService service) : ControllerBase
{
    /// <summary>Lists an employee's assignments, optionally filtered by component and active date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeePayrollComponentAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeePayrollComponentAssignmentDto>>> GetAssignments(
        Guid employeeId, [FromQuery] Guid? payrollComponentId, [FromQuery] DateOnly? activeOn, CancellationToken ct)
    {
        var result = await service.GetEmployeeAssignmentsAsync(employeeId, payrollComponentId, activeOn, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeePayrollComponentAssignmentDto>>(result.Failure!);
    }

    /// <summary>Returns one assignment with the current component summary, including inactive components.</summary>
    [HttpGet("{assignmentId:guid}")]
    [ProducesResponseType(typeof(EmployeePayrollComponentAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePayrollComponentAssignmentDto>> GetAssignment(Guid employeeId, Guid assignmentId, CancellationToken ct)
    {
        var result = await service.GetEmployeeAssignmentAsync(employeeId, assignmentId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePayrollComponentAssignmentDto>(result.Failure!);
    }

    /// <summary>Creates an effective-dated assignment for an active payroll component.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeePayrollComponentAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeePayrollComponentAssignmentDto>> CreateAssignment(
        Guid employeeId, [FromBody] EmployeePayrollComponentAssignmentRequest request, CancellationToken ct)
    {
        var result = await service.CreateEmployeeAssignmentAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeePayrollComponentAssignmentDto>(result.Failure!);
        return CreatedAtAction(nameof(GetAssignment), new { employeeId, assignmentId = result.Value!.EmployeePayrollComponentAssignmentId }, result.Value);
    }

    /// <summary>Updates an assignment while preserving employee ownership and checking the effective-date overlap rule.</summary>
    [HttpPut("{assignmentId:guid}")]
    [ProducesResponseType(typeof(EmployeePayrollComponentAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeePayrollComponentAssignmentDto>> UpdateAssignment(
        Guid employeeId, Guid assignmentId, [FromBody] EmployeePayrollComponentAssignmentRequest request, CancellationToken ct)
    {
        var result = await service.UpdateEmployeeAssignmentAsync(employeeId, assignmentId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePayrollComponentAssignmentDto>(result.Failure!);
    }

    /// <summary>Deletes an employee payroll component assignment.</summary>
    [HttpDelete("{assignmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssignment(Guid employeeId, Guid assignmentId, CancellationToken ct)
    {
        var result = await service.DeleteEmployeeAssignmentAsync(employeeId, assignmentId, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = StatusCodes.Status404NotFound })),
        "conflict" => new(Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: failure.Message)),
        _ => new(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
