using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Provides a paginated global view of effective-dated employee payroll component assignments.</summary>
[ApiController]
[Route("api/payroll-component-assignments")]
[Produces("application/json")]
public sealed class PayrollComponentAssignmentsController(IEmployeePayrollComponentAssignmentService service) : ControllerBase
{
    /// <summary>Lists assignments with optional employee, component and active-date filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EmployeePayrollComponentAssignmentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EmployeePayrollComponentAssignmentListItemDto>>> GetAssignments(
        [FromQuery] EmployeePayrollComponentAssignmentListQuery query, CancellationToken ct)
    {
        var result = await service.GetAssignmentsAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PagedResult<EmployeePayrollComponentAssignmentListItemDto>>(result.Failure!);
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        _ => new(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
