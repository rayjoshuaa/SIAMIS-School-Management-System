using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

[ApiController]
[Route("api/employees/{employeeId:guid}")]
[Produces("application/json")]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public sealed class EmploymentLifecycleController(IEmploymentLifecycleService lifecycle, IEmploymentResolver resolver, IEmployeeAccountLifecycleService accounts) : ControllerBase
{
    /// <summary>Closes the current context the day before EffectiveDate and opens its replacement. Omitted IDs retain their values; future dates are rejected.</summary>
    [HttpPost("employment-changes")]
    [ProducesResponseType(typeof(EmploymentRecordDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Change(Guid employeeId, EmploymentChangeRequest request, CancellationToken ct)
    {
        var result = await lifecycle.ChangeAsync(employeeId, request, ct);
        return result.IsSuccess ? Created($"/api/employees/{employeeId}/employment-history", result.Value) : Failure(result.Failure!);
    }

    /// <summary>Ends the expected current employment inclusively. An active linked account requires explicit DisableLinkedAccount, its current version and Security.Manage. Future dates are rejected; employee and account history are retained.</summary>
    [HttpPost("end-employment")]
    [ProducesResponseType(typeof(EmploymentRecordDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> End(Guid employeeId, EndEmploymentRequest request, CancellationToken ct)
    {
        var result = await lifecycle.EndAsync(employeeId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }

    /// <summary>Rehires the same employee with a new non-overlapping open record. Future employment starts are rejected.</summary>
    [HttpPost("rehire")]
    [ProducesResponseType(typeof(EmploymentRecordDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Rehire(Guid employeeId, RehireRequest request, CancellationToken ct)
    {
        var result = await lifecycle.RehireAsync(employeeId, request, ct);
        return result.IsSuccess ? Created($"/api/employees/{employeeId}/employment-history", result.Value) : Failure(result.Failure!);
    }

    /// <summary>Returns employment history in employment-start order without editing historical records.</summary>
    [HttpGet("employment-history")]
    [ProducesResponseType(typeof(IReadOnlyList<EmploymentRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(Guid employeeId, CancellationToken ct)
    {
        var result = await lifecycle.HistoryAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }

    /// <summary>Resolves employment on the supplied date using inclusive EndDate, independent of IsCurrent. Returns null for a gap and 409 for ambiguous history.</summary>
    [HttpGet("employment-effective")]
    [ProducesResponseType(typeof(EmploymentRecordDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Effective(Guid employeeId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var result = await resolver.ResolveAsync(employeeId, date, ct);
        return result.IsSuccess ? new JsonResult(result.Value) : Failure(result.Failure!);
    }

    /// <summary>Safe HR account linkage/status and employment readiness; never returns security stamps or credentials.</summary>
    [HttpGet("account-lifecycle")]
    [ProducesResponseType(typeof(EmployeeAccountLifecycleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AccountLifecycle(Guid employeeId, CancellationToken ct)
    {
        var result = await accounts.GetAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }

    private IActionResult Failure(ApiFailure f) => f.Code switch
    {
        "validation" => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [f.Message] }) { Status = 400 }),
        "not_found" => Problem(statusCode: 404, title: "Not found", detail: f.Message),
        "conflict" or "active_linked_account_requires_offboarding_decision" or "last_usable_system_admin_required" => StatusCode(409, new ProblemDetails { Status = 409, Title = "Conflict", Detail = f.Message, Extensions = { ["code"] = f.Code } }),
        "forbidden" => StatusCode(403, new ProblemDetails { Status = 403, Title = "Forbidden", Detail = f.Message }),
        _ => Problem(statusCode: 500)
    };
}
