using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>HTTP mapping shared by the two focused statutory configuration controllers.</summary>
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public abstract class StatutoryConfigurationController : ControllerBase
{
    protected ActionResult<T> Respond<T>(ServiceResult<T> result) => result.IsSuccess ? Ok(result.Value) : Failure<T>(result.Failure!);
    protected ActionResult<T> Failure<T>(ApiFailure f) => f.Code switch {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string,string[]> { ["request"] = [f.Message] }) { Status = 400 })),
        "not_found" => new(Problem(statusCode: 404, title: "Not found", detail: f.Message)),
        "conflict" => new(Problem(statusCode: 409, title: "Conflict", detail: f.Message)),
        _ => new(Problem(statusCode: 500, title: "Unexpected error"))
    };
}
