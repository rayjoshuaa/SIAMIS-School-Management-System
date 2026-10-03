using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;

namespace SIAMIS.Api.Controllers;

/// <summary>D8D Development-only external evidence receipts and sandwich review. No binary delivery or authenticated reviewer attribution.</summary>
[ApiController]
[Produces("application/json")]
public sealed class LeaveEvidenceSandwichController(ILeaveEvidenceSandwichService service, IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Frozen required types, immutable external receipts, history and exact approval evidence IDs. Development only.</summary>
    [HttpGet("api/employees/{employeeId:guid}/leave/{leaveId:guid}/evidence")]
    [ProducesResponseType(typeof(LeaveEvidenceSummary), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeaveEvidenceSummary>> Evidence(Guid employeeId, Guid leaveId, CancellationToken ct)
        => !environment.IsDevelopment() ? NotFound() : Result(await service.EvidenceAsync(employeeId, leaveId, ct));

    /// <summary>Records an external review receipt token or immutable successor, without uploading files. Development only.</summary>
    [HttpPost("api/employees/{employeeId:guid}/leave/{leaveId:guid}/evidence")]
    [ProducesResponseType(typeof(LeaveEvidenceDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeaveEvidenceDto>> Record(Guid employeeId, Guid leaveId, LeaveEvidenceRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var r = await service.RecordEvidenceAsync(employeeId, leaveId, request, ct);
        return r.IsSuccess ? CreatedAtAction(nameof(Evidence), new { employeeId, leaveId }, r.Value) : Result(r);
    }

    /// <summary>Accepts one current Recorded external evidence version with review remarks and server UTC time. Development only.</summary>
    [HttpPost("api/employees/{employeeId:guid}/leave/{leaveId:guid}/evidence/{evidenceId:guid}/accept")]
    [ProducesResponseType(typeof(LeaveEvidenceDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeaveEvidenceDto>> Accept(Guid employeeId, Guid leaveId, Guid evidenceId, LeaveEvidenceReviewRequest request, CancellationToken ct)
        => !environment.IsDevelopment() ? NotFound() : Result(await service.ReviewEvidenceAsync(employeeId, leaveId, evidenceId, true, request, ct));

    /// <summary>Rejects a Recorded external evidence version, preserving its immutable receipt/history. Development only.</summary>
    [HttpPost("api/employees/{employeeId:guid}/leave/{leaveId:guid}/evidence/{evidenceId:guid}/reject")]
    [ProducesResponseType(typeof(LeaveEvidenceDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeaveEvidenceDto>> Reject(Guid employeeId, Guid leaveId, Guid evidenceId, LeaveEvidenceReviewRequest request, CancellationToken ct)
        => !environment.IsDevelopment() ? NotFound() : Result(await service.ReviewEvidenceAsync(employeeId, leaveId, evidenceId, false, request, ct));

    /// <summary>Frozen sandwich cases, separate debit dates/year allocations, classification and review history. Development only.</summary>
    [HttpGet("api/employees/{employeeId:guid}/leave-sandwich-cases")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveSandwichDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<IReadOnlyList<LeaveSandwichDto>>> Cases(Guid employeeId, [FromQuery] Guid? leaveId, CancellationToken ct)
        => !environment.IsDevelopment() ? NotFound() : Result(await service.SandwichesAsync(employeeId, leaveId, ct));

    /// <summary>HR sandwich review with explicit Outcome, ExpectedStatus=ReviewPending, mandatory reason and server UTC time. Development only.</summary>
    [HttpPost("api/employees/{employeeId:guid}/leave-sandwich-cases/{caseId:guid}/review")]
    [ProducesResponseType(typeof(LeaveSandwichDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeaveSandwichDto>> Review(Guid employeeId, Guid caseId, LeaveSandwichReviewRequest request, CancellationToken ct)
        => !environment.IsDevelopment() ? NotFound() : Result(await service.ReviewSandwichAsync(employeeId, caseId, request, ct));

    private ActionResult<T> Result<T>(ServiceResult<T> r) => r.IsSuccess ? Ok(r.Value) : r.Failure!.Code switch
    {
        "validation" => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [r.Failure.Message] }) { Status = 400 }),
        "not_found" => NotFound(new ProblemDetails { Status = 404, Title = "Not found", Detail = r.Failure.Message }),
        "conflict" => StatusCode(409, new ProblemDetails { Status = 409, Title = "Conflict", Detail = r.Failure.Message }),
        _ => Problem(statusCode: 500)
    };
}
