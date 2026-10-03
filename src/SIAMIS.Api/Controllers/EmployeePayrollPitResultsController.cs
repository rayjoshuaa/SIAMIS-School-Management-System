using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Focused read of immutable PIT calculation evidence; omitted from broad employee lists.</summary>
[Route("api/employee-payrolls/{payrollId:guid}/pit-result")]
public sealed class EmployeePayrollPitResultsController(IPitPayrollService service) : StatutoryConfigurationController
{
    /// <summary>Reads the stored PIT result without recomputing; null when PIT was explicitly NotApplicable.</summary>
    [HttpGet] [ProducesResponseType(typeof(EmployeePayrollPitResultDto), 200)] [ProducesResponseType(404)]
    public async Task<ActionResult<EmployeePayrollPitResultDto?>> Get(Guid payrollId, CancellationToken ct)
    {
        var result = await service.GetResultAsync(payrollId, ct);
        return result.IsSuccess ? new JsonResult(result.Value) { StatusCode = 200 } : Failure<EmployeePayrollPitResultDto?>(result.Failure!);
    }
}
