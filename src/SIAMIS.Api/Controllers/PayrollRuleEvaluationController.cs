using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Development diagnostic endpoint for resolving payroll rules that match an employee and payroll period. It does not calculate payroll amounts.</summary>
[ApiController]
[Route("api/payroll-rules/evaluation")]
[Produces("application/json")]
public sealed class PayrollRuleEvaluationController(IPayrollRuleEvaluator evaluator, IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Returns effective, target-matched rules with employment context at the employee's first eligible point in the payroll period.</summary>
    [HttpGet("{payrollPeriodId:guid}/{employeeId:guid}")]
    [ProducesResponseType(typeof(PayrollRuleEvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRuleEvaluationDto>> Evaluate(Guid payrollPeriodId, Guid employeeId, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var result = await evaluator.EvaluateApplicableRulesAsync(payrollPeriodId, employeeId, ct);
        if (result.IsSuccess) return Ok(result.Value);
        if (result.Failure!.Code == "validation")
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [result.Failure.Message] }) { Status = 400 });
        if (result.Failure.Code == "conflict")
            return Problem(statusCode: 409, title: "Employment history conflict", detail: result.Failure.Message);
        return NotFound(new ProblemDetails
        {
            Title = "Not found",
            Detail = result.Failure!.Message,
            Status = StatusCodes.Status404NotFound
        });
    }
}
