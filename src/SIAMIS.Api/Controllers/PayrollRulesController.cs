using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages generic effective-dated payroll rule configuration; rules are not executed by payroll calculation.</summary>
[ApiController]
[Route("api/payroll-rules")]
[Produces("application/json")]
public sealed class PayrollRulesController(IPayrollRuleService service) : ControllerBase
{
    /// <summary>Lists rules with filters and pagination. Active rules are returned by default, ordered by Priority, Name, then PayrollRuleId. This endpoint returns configuration only and does not execute rules.</summary>
    /// <param name="query">Optional filters include componentId and applicationMode (Supplement or ReplaceAssignment).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PayrollRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PayrollRuleDto>>> GetPayrollRules([FromQuery] PayrollRuleListQuery query, CancellationToken ct)
    {
        var result = await service.GetPayrollRulesAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PagedResult<PayrollRuleDto>>(result.Failure!);
    }

    /// <summary>Returns the full configuration for one payroll rule.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PayrollRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRuleDto>> GetPayrollRule(Guid id, CancellationToken ct)
    {
        var rule = await service.GetPayrollRuleAsync(id, ct);
        return rule is null ? NotFoundProblem() : Ok(rule);
    }

    /// <summary>Creates a rule linked to one active, stage-compatible payroll component. ApplicationMode defaults to Supplement. Rule configuration is not executed by payroll calculation yet.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PayrollRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRuleDto>> CreatePayrollRule([FromBody] PayrollRuleRequest request, CancellationToken ct)
    {
        var result = await service.CreatePayrollRuleAsync(request, ct);
        if (!result.IsSuccess) return Failure<PayrollRuleDto>(result.Failure!);
        return CreatedAtAction(nameof(GetPayrollRule), new { id = result.Value!.PayrollRuleId }, result.Value);
    }

    /// <summary>Updates a rule and its active, stage-compatible payroll component and application mode without changing the rule ID.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PayrollRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRuleDto>> UpdatePayrollRule(Guid id, [FromBody] PayrollRuleRequest request, CancellationToken ct)
    {
        var result = await service.UpdatePayrollRuleAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PayrollRuleDto>(result.Failure!);
    }

    /// <summary>Changes only the active state of a payroll rule.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] PayrollRuleStatusRequest request, CancellationToken ct)
    {
        var result = await service.SetPayrollRuleStatusAsync(id, request.IsActive!.Value, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Deletes a payroll rule configuration.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayrollRule(Guid id, CancellationToken ct)
    {
        var result = await service.DeletePayrollRuleAsync(id, ct);
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

    private ObjectResult NotFoundProblem() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Payroll rule was not found.");
}
