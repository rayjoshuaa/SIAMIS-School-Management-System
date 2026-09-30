using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages stored payroll snapshots. Line mutations reconcile derived header totals from stored snapshots while preserving BasicSalary.</summary>
[ApiController]
[Route("api/employee-payrolls")]
[Produces("application/json")]
public sealed class EmployeePayrollsController(IEmployeePayrollService service) : ControllerBase
{
    /// <summary>Returns paginated employee payroll records with optional employee, period and status filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeePayrollListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeePayrollListItemDto>>> GetPayrolls([FromQuery] EmployeePayrollListQuery query, CancellationToken ct)
    {
        var result = await service.GetPayrollsAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PagedResult<EmployeePayrollListItemDto>>(result.Failure!);
    }

    /// <summary>Returns the payroll header, employee and period summaries, and snapshot lines.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeePayrollDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePayrollDetailDto>> GetPayroll(Guid id, CancellationToken ct)
    {
        var result = await service.GetPayrollAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePayrollDetailDto>(result.Failure!);
    }

    /// <summary>Creates an empty Draft payroll header. All financial totals are server-managed and start at zero.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeePayrollDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeePayrollDetailDto>> CreatePayroll([FromBody] EmployeePayrollCreateRequest request, CancellationToken ct)
    {
        var result = await service.CreatePayrollAsync(request, ct);
        if (!result.IsSuccess) return Failure<EmployeePayrollDetailDto>(result.Failure!);
        return CreatedAtAction(nameof(GetPayroll), new { id = result.Value!.Payroll.EmployeePayrollId }, result.Value);
    }

    /// <summary>Updates Draft/Calculated remarks. Employee and period may change only on an empty Draft. Financial and lifecycle values are server-managed.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeePayrollDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeePayrollDetailDto>> UpdatePayroll(Guid id, [FromBody] EmployeePayrollUpdateRequest request, CancellationToken ct)
    {
        var result = await service.UpdatePayrollAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePayrollDetailDto>(result.Failure!);
    }

    /// <summary>Approves a Calculated payroll after validating stored financial integrity.</summary>
    /// <remarks>Authorization and authenticated actor attribution must be added when SIAMIS authentication is implemented.</remarks>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await service.ApprovePayrollAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Marks an Approved payroll Paid after validating its approval timestamp and stored financial integrity.</summary>
    /// <remarks>Authorization and authenticated actor attribution must be added when SIAMIS authentication is implemented.</remarks>
    [HttpPost("{id:guid}/mark-paid")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkPaid(Guid id, CancellationToken ct)
    {
        var result = await service.MarkPayrollPaidAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Cancels a Draft/Calculated payroll with a required reason, preserving financial totals and lines.</summary>
    /// <remarks>Authorization and authenticated actor attribution must be added when SIAMIS authentication is implemented.</remarks>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] EmployeePayrollCancelRequest request, CancellationToken ct)
    {
        var result = await service.CancelPayrollAsync(id, request, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Deletes only an empty Draft payroll.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePayroll(Guid id, CancellationToken ct)
    {
        var result = await service.DeletePayrollAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Lists the snapshot lines for a payroll.</summary>
    [HttpGet("{payrollId:guid}/lines")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeePayrollLineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeePayrollLineDto>>> GetLines(Guid payrollId, CancellationToken ct)
    {
        var result = await service.GetLinesAsync(payrollId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeePayrollLineDto>>(result.Failure!);
    }

    /// <summary>Returns a payroll line belonging to the specified payroll.</summary>
    [HttpGet("{payrollId:guid}/lines/{lineId:guid}")]
    [ProducesResponseType(typeof(EmployeePayrollLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeePayrollLineDto>> GetLine(Guid payrollId, Guid lineId, CancellationToken ct)
    {
        var result = await service.GetLineAsync(payrollId, lineId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePayrollLineDto>(result.Failure!);
    }

    /// <summary>Adds a Manual line to Draft/Calculated payroll and atomically reconciles derived totals from stored snapshots, preserving BasicSalary.</summary>
    [HttpPost("{payrollId:guid}/lines")]
    [ProducesResponseType(typeof(EmployeePayrollLineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeePayrollLineDto>> CreateLine(Guid payrollId, [FromBody] EmployeePayrollLineRequest request, CancellationToken ct)
    {
        var result = await service.CreateLineAsync(payrollId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeePayrollLineDto>(result.Failure!);
        return CreatedAtAction(nameof(GetLine), new { payrollId, lineId = result.Value!.EmployeePayrollLineId }, result.Value);
    }

    /// <summary>Updates only a Manual line in Draft/Calculated payroll and reconciles derived totals. Same-component classification is preserved; a changed component refreshes classification.</summary>
    [HttpPut("{payrollId:guid}/lines/{lineId:guid}")]
    [ProducesResponseType(typeof(EmployeePayrollLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeePayrollLineDto>> UpdateLine(Guid payrollId, Guid lineId, [FromBody] EmployeePayrollLineRequest request, CancellationToken ct)
    {
        var result = await service.UpdateLineAsync(payrollId, lineId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeePayrollLineDto>(result.Failure!);
    }

    /// <summary>Deletes only a Manual line in Draft/Calculated payroll and atomically reconciles derived totals, preserving BasicSalary.</summary>
    [HttpDelete("{payrollId:guid}/lines/{lineId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteLine(Guid payrollId, Guid lineId, CancellationToken ct)
    {
        var result = await service.DeleteLineAsync(payrollId, lineId, ct);
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
