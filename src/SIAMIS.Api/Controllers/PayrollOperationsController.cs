using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Reads authoritative stored payroll facts without executing payroll calculators.</summary>
[ApiController]
[Produces("application/json")]
public sealed class PayrollOperationsController(IPayrollOperationsService service) : ControllerBase
{
    /// <summary>Returns a structured frozen payslip before or after payment; retained cancelled snapshots include current lifecycle status.</summary>
    /// <remarks>Draft, missing configuration/evidence, and inconsistent snapshots return 409. No PDF is generated.</remarks>
    [HttpGet("api/employee-payrolls/{id:guid}/payslip")]
    [ProducesResponseType(typeof(PayslipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayslipDto>> Payslip(Guid id, CancellationToken ct)
        => Respond(await service.GetPayslipAsync(id, ct));

    /// <summary>Returns stored-integrity findings, payslip readiness and approval readiness; does not calculate or approve payroll.</summary>
    [HttpGet("api/employee-payrolls/{id:guid}/review")]
    [ProducesResponseType(typeof(PayrollReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollReviewDto>> Review(Guid id, CancellationToken ct)
        => Respond(await service.ReviewAsync(id, ct));

    /// <summary>Aggregates all represented payrolls, including Cancelled, by stored D3 currency. Missing currency is counted separately.</summary>
    /// <remarks>Status counts are period-wide. No expected population, historical generation failures or currency conversion are inferred.</remarks>
    [HttpGet("api/payroll-periods/{id:guid}/summary")]
    [ProducesResponseType(typeof(PayrollPeriodOperationsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollPeriodOperationsDto>> Summary(Guid id, CancellationToken ct)
        => Respond(await service.GetPeriodSummaryAsync(id, ct));

    private ActionResult<T> Respond<T>(ServiceResult<T> result) => result.IsSuccess ? Ok(result.Value)
        : Problem(statusCode: result.Failure!.Code == "not_found" ? 404 : 409,
            title: result.Failure.Code == "not_found" ? "Not found" : "Not ready", detail: result.Failure.Message);
}
