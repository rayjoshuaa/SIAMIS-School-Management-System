using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.MasterData;

namespace SIAMIS.Api.Controllers;

[ApiController]
[Route("api/master-data/employment-statuses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public sealed class EmploymentStatusesController(IEmploymentStatusService statuses) : ControllerBase
{
    /// <summary>Creates a configurable employment status with explicit terminal classification.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmploymentStatusDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(EmploymentStatusRequest request, CancellationToken ct)
    {
        var result = await statuses.SaveAsync(null, request, ct);
        return result.IsSuccess ? Created("/api/master-data/employment-statuses", result.Value) : Failure(result.Failure!);
    }

    /// <summary>Updates master data. Terminal classification cannot change once referenced by employment history; display changes remain allowed.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmploymentStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, EmploymentStatusRequest request, CancellationToken ct)
    {
        var result = await statuses.SaveAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }

    private IActionResult Failure(ApiFailure f) => f.Code switch
    {
        "validation" => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [f.Message] }) { Status = 400 }),
        "not_found" => Problem(statusCode: 404, title: "Not found", detail: f.Message),
        "conflict" => Problem(statusCode: 409, title: "Conflict", detail: f.Message),
        _ => Problem(statusCode: 500)
    };
}
