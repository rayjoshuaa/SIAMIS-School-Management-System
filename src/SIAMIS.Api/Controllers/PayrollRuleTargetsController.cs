using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Configures employee, department, designation, employment type, and location targets for payroll rules.</summary>
[ApiController]
[Route("api/payroll-rules/{payrollRuleId:guid}/targets")]
[Produces("application/json")]
public sealed class PayrollRuleTargetsController(IPayrollRuleTargetService service) : ControllerBase
{
    /// <summary>Lists a payroll rule's targeting criteria in stable inclusion, type, name, and ID order. No targets means the rule is configured for all employees.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PayrollRuleTargetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PayrollRuleTargetDto>>> GetTargets(Guid payrollRuleId, CancellationToken ct)
    {
        var result = await service.GetTargetsAsync(payrollRuleId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<PayrollRuleTargetDto>>(result.Failure!);
    }

    /// <summary>Returns one target belonging to the specified payroll rule.</summary>
    [HttpGet("{targetId:guid}")]
    [ProducesResponseType(typeof(PayrollRuleTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollRuleTargetDto>> GetTarget(Guid payrollRuleId, Guid targetId, CancellationToken ct)
    {
        var target = await service.GetTargetAsync(payrollRuleId, targetId, ct);
        return target is null ? NotFoundProblem("Payroll rule target was not found.") : Ok(target);
    }

    /// <summary>Adds an inclusion or exclusion target. Employee targets may refer to inactive employees; master-data targets must be active.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PayrollRuleTargetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRuleTargetDto>> CreateTarget(Guid payrollRuleId, [FromBody] PayrollRuleTargetRequest request, CancellationToken ct)
    {
        var result = await service.CreateTargetAsync(payrollRuleId, request, ct);
        if (!result.IsSuccess) return Failure<PayrollRuleTargetDto>(result.Failure!);
        return CreatedAtAction(nameof(GetTarget), new { payrollRuleId, targetId = result.Value!.PayrollRuleTargetId }, result.Value);
    }

    /// <summary>Changes the target type, target ID, or exclusion state without changing its payroll rule parent.</summary>
    [HttpPut("{targetId:guid}")]
    [ProducesResponseType(typeof(PayrollRuleTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRuleTargetDto>> UpdateTarget(Guid payrollRuleId, Guid targetId,
        [FromBody] PayrollRuleTargetRequest request, CancellationToken ct)
    {
        var result = await service.UpdateTargetAsync(payrollRuleId, targetId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PayrollRuleTargetDto>(result.Failure!);
    }

    /// <summary>Deletes only this target. Deleting the final target leaves the rule with no targets, which represents all employees.</summary>
    [HttpDelete("{targetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTarget(Guid payrollRuleId, Guid targetId, CancellationToken ct)
    {
        var result = await service.DeleteTargetAsync(payrollRuleId, targetId, ct);
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

    private ObjectResult NotFoundProblem(string detail)
        => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail);
}
