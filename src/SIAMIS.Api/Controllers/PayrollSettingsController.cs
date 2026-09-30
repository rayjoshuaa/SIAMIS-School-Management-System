using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages organization-wide payroll defaults. These settings are configuration only and are not yet consumed by calculation.</summary>
[ApiController]
[Route("api/payroll-settings")]
[Produces("application/json")]
public sealed class PayrollSettingsController(IPayrollSettingsService service) : ControllerBase
{
    /// <summary>Returns the current active global payroll settings, or 404 when none are configured.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PayrollSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollSettingsDto>> GetCurrent(CancellationToken ct)
    {
        var settings = await service.GetCurrentPayrollSettingsAsync(ct);
        return settings is null ? NotFoundProblem() : Ok(settings);
    }

    /// <summary>Returns a settings record by ID, including inactive records.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PayrollSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollSettingsDto>> GetById(Guid id, CancellationToken ct)
    {
        var settings = await service.GetPayrollSettingsAsync(id, ct);
        return settings is null ? NotFoundProblem() : Ok(settings);
    }

    /// <summary>Creates explicitly supplied global payroll settings. IsActive defaults to true when omitted.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PayrollSettingsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollSettingsDto>> Create([FromBody] PayrollSettingsRequest request, CancellationToken ct)
    {
        var result = await service.CreatePayrollSettingsAsync(request, ct);
        if (!result.IsSuccess) return Failure<PayrollSettingsDto>(result.Failure!);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.PayrollSettingsId }, result.Value);
    }

    /// <summary>Updates a settings record. Omitted IsActive preserves the current active state.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PayrollSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollSettingsDto>> Update(Guid id, [FromBody] PayrollSettingsRequest request, CancellationToken ct)
    {
        var result = await service.UpdatePayrollSettingsAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PayrollSettingsDto>(result.Failure!);
    }

    /// <summary>Activates or deactivates this settings record. Only one record may be active.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] PayrollSettingsStatusRequest request, CancellationToken ct)
    {
        var result = await service.SetPayrollSettingsStatusAsync(id, request.IsActive!.Value, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Deletes a payroll settings record. There are no payroll record references to settings yet.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeletePayrollSettingsAsync(id, ct);
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

    private ObjectResult NotFoundProblem() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Active Payroll Settings were not found.");
}
