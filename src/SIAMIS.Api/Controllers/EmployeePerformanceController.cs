using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages employee performance records without appraisal workflows or KPI processing.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/performance")]
[Produces("application/json")]
public sealed class EmployeePerformanceController(IEmployeePerformanceService service) : ControllerBase
{
    /// <summary>Returns performance records, optionally filtered by review date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeePerformanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeePerformanceDto>>> GetPerformanceRecords(
        Guid employeeId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        var result = await service.GetPerformanceRecordsAsync(employeeId, fromDate, toDate, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeePerformanceDto>>(result.Failure!);
    }

    /// <summary>Returns one performance record belonging to the employee.</summary>
    [HttpGet("{performanceRecordId:guid}")]
    [ProducesResponseType(typeof(EmployeePerformanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePerformanceDto>> GetPerformanceRecord(Guid employeeId, Guid performanceRecordId, CancellationToken ct)
    {
        var result = await service.GetPerformanceRecordAsync(employeeId, performanceRecordId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePerformanceDto>(result.Failure!);
    }

    /// <summary>Creates a performance record for the employee.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeePerformanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePerformanceDto>> CreatePerformanceRecord(Guid employeeId, [FromBody] EmployeePerformanceRequest request, CancellationToken ct)
    {
        var result = await service.CreatePerformanceRecordAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeePerformanceDto>(result.Failure!);
        return CreatedAtAction(nameof(GetPerformanceRecord), new { employeeId, performanceRecordId = result.Value!.PerformanceRecordId }, result.Value);
    }

    /// <summary>Updates a performance record without creating a new record.</summary>
    [HttpPut("{performanceRecordId:guid}")]
    [ProducesResponseType(typeof(EmployeePerformanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePerformanceDto>> UpdatePerformanceRecord(Guid employeeId, Guid performanceRecordId, [FromBody] EmployeePerformanceRequest request, CancellationToken ct)
    {
        var result = await service.UpdatePerformanceRecordAsync(employeeId, performanceRecordId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePerformanceDto>(result.Failure!);
    }

    /// <summary>Deletes a performance record without affecting employee data.</summary>
    [HttpDelete("{performanceRecordId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePerformanceRecord(Guid employeeId, Guid performanceRecordId, CancellationToken ct)
    {
        var result = await service.DeletePerformanceRecordAsync(employeeId, performanceRecordId, ct);
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
