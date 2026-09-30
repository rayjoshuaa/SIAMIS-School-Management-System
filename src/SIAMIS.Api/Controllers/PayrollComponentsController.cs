using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.MasterData;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages configurable earning and deduction components as master data.</summary>
[ApiController]
[Route("api/payroll-components")]
[Produces("application/json")]
public sealed class PayrollComponentsController(IPayrollComponentService service) : ControllerBase
{
    /// <summary>Lists payroll components with optional type, inactive and text filters.</summary>
    /// <param name="componentType">Optional component type: Earning or Deduction.</param>
    /// <param name="includeInactive">When true, includes inactive components.</param>
    /// <param name="search">Optional search term matched against code, name or description.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PayrollComponentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PayrollComponentDto>>> GetPayrollComponents(
        [FromQuery] string? componentType, [FromQuery] bool includeInactive = false, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var result = await service.GetPayrollComponentsAsync(componentType, includeInactive, search, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<PayrollComponentDto>>(result.Failure!);
    }

    /// <summary>Returns one payroll component by its identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PayrollComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollComponentDto>> GetPayrollComponent(Guid id, CancellationToken ct)
    {
        var item = await service.GetPayrollComponentAsync(id, ct);
        return item is null ? NotFoundProblem() : Ok(item);
    }

    /// <summary>Creates a payroll component. Percentage components require a PercentageBase (BasicSalary, GrossEarnings or GrossPay); other methods require it to be null.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PayrollComponentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollComponentDto>> CreatePayrollComponent([FromBody] PayrollComponentRequest request, CancellationToken ct)
    {
        var result = await service.CreatePayrollComponentAsync(request, ct);
        if (!result.IsSuccess) return Failure<PayrollComponentDto>(result.Failure!);
        return CreatedAtAction(nameof(GetPayrollComponent), new { id = result.Value!.PayrollComponentId }, result.Value);
    }

    /// <summary>Updates code, name, type, description, calculation method and percentage base without changing active status. PercentageBase is configuration only and does not calculate an amount.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PayrollComponentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollComponentDto>> UpdatePayrollComponent(Guid id, [FromBody] PayrollComponentRequest request, CancellationToken ct)
    {
        var result = await service.UpdatePayrollComponentAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PayrollComponentDto>(result.Failure!);
    }

    /// <summary>Changes only the active status of a payroll component.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] PayrollComponentStatusRequest request, CancellationToken ct)
    {
        var result = await service.SetPayrollComponentStatusAsync(id, request.IsActive!.Value, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Deletes a payroll component master-data record.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayrollComponent(Guid id, CancellationToken ct)
    {
        var result = await service.DeletePayrollComponentAsync(id, ct);
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

    private ObjectResult NotFoundProblem() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Payroll component was not found.");
}
