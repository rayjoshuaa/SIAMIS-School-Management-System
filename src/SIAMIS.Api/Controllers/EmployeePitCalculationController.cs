using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Employee-scoped advisory PIT evaluation; never persists tax or changes payroll totals.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/payroll-periods/{payrollPeriodId:guid}/pit-preview")]
public sealed class EmployeePitCalculationController(IPitCalculationPreviewService service) : ControllerBase
{
    /// <summary>Resolves PIT-TH-V1 inputs for PayDate. Missing schedule/history authority returns RequiresReview, never assumed tax.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PitCalculationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PitCalculationResult>> Get(Guid employeeId, Guid payrollPeriodId, CancellationToken ct)
    {
        var result = await service.PreviewAsync(employeeId, payrollPeriodId, ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(statusCode: 404, title: result.Failure!.Message);
    }
}
