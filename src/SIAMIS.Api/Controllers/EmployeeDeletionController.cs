using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;
[ApiController, Route("api/employees/{id:guid}/permanent-deletion")]
public sealed class EmployeeDeletionController(IEmployeeDeletionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Assess(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await service.AssessAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }
    // POST makes the confirmed command explicit; never a navigation GET or a generic employee DELETE.
    [HttpPost]
    public async Task<IActionResult> Delete(Guid id, [FromBody] EmployeeDeletionRequest request, CancellationToken ct)
    {
        var result = await service.DeleteAsync(id, request, ct);
        return result.IsSuccess ? NoContent() : Failure(result.Failure!);
    }
    private ObjectResult Failure(ApiFailure failure) => StatusCode(failure.Code switch
    { "forbidden" => 403, "not_found" => 404, "validation" => 400, "conflict" => 409, _ => 503 },
        new ProblemDetails { Title = "Employee deletion unavailable", Detail = failure.Message });
}
