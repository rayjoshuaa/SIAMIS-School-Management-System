using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Attendance review and immutable finalization. All mutations are Development-only and unattributed until authentication exists.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/attendance-days/{date}")]
[Produces("application/json")]
public sealed class AttendanceReviewController(IAttendanceReviewService service, IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Reads raw/adjudicated calculations, original findings, history, current source/version tokens and latest historical revision. IsStale/RequiresReopen never mutate sources. IsCurrentlyValidated is false for stale or reopened days.</summary>
    [HttpGet("review")]
    [ProducesResponseType(typeof(AttendanceReviewDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<AttendanceReviewDto>> Review(Guid employeeId, DateOnly date, CancellationToken ct) => Result(await service.ReadAsync(employeeId, date, ct));

    /// <summary>Reads immutable finalized revisions in revision order, independent of mutable current configuration. Historical records are not assertions of current validity; use review for that.</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<FinalizedAttendanceRevisionDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<IReadOnlyList<FinalizedAttendanceRevisionDto>>> History(Guid employeeId, DateOnly date, CancellationToken ct) => Result(await service.HistoryAsync(employeeId, date, ct));

    /// <summary>Development-only addition of reasoned ManualAuthorized IN/OUT correction evidence. Preserves raw events. Requires current review ExpectedVersion/ExpectedSourceFingerprint and a unique ManualRequestKey.</summary>
    [HttpPost("corrections")]
    [ProducesResponseType(typeof(AttendanceReviewDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public Task<ActionResult<AttendanceReviewDto>> Correction(Guid employeeId, DateOnly date, AttendanceCorrectionRequest request, CancellationToken ct) => Mutation(employeeId, date, "CorrectionAdded", request, ct, true);

    /// <summary>Development-only append-only Included/Excluded decision for owned date evidence. Requires reason and current tokens. Does not delete an event or decide Leave/presence precedence.</summary>
    [HttpPost("adjudications")]
    [ProducesResponseType(typeof(AttendanceReviewDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public Task<ActionResult<AttendanceReviewDto>> Adjudication(Guid employeeId, DateOnly date, AttendanceAdjudicationRequest request, CancellationToken ct) => Mutation(employeeId, date, "Adjudication", request, ct, true);

    /// <summary>Development-only explicit confirmation of unambiguous PotentialAbsence with reason and current tokens. Has no Leave/payroll/KPI effect. Confirmation is invalidated by changed sources or reopening.</summary>
    [HttpPost("confirm-absence")]
    [ProducesResponseType(typeof(AttendanceReviewDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public Task<ActionResult<AttendanceReviewDto>> Confirm(Guid employeeId, DateOnly date, AttendanceReviewRequest request, CancellationToken ct) => Mutation(employeeId, date, "AbsenceConfirmed", request, ct, false);

    /// <summary>Development-only server recalculation/freeze into a new immutable revision. Requires reason, current tokens, resolved blocking findings and explicit absence confirmation if applicable. Already finalized days require reopen; there is no force finalize.</summary>
    [HttpPost("finalize")]
    [ProducesResponseType(typeof(AttendanceReviewDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public Task<ActionResult<AttendanceReviewDto>> FinalizeDay(Guid employeeId, DateOnly date, AttendanceReviewRequest request, CancellationToken ct) => Mutation(employeeId, date, "Finalized", request, ct, true);

    /// <summary>Development-only explicit audited reopening with reason and current tokens. Preserves the previous finalized revision; a competing/stale reopen returns 409. Does not automatically refinalize or mutate Leave.</summary>
    [HttpPost("reopen")]
    [ProducesResponseType(typeof(AttendanceReviewDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public Task<ActionResult<AttendanceReviewDto>> Reopen(Guid employeeId, DateOnly date, AttendanceReviewRequest request, CancellationToken ct) => Mutation(employeeId, date, "Reopened", request, ct, false);
    private async Task<ActionResult<AttendanceReviewDto>> Mutation(Guid employeeId, DateOnly date, string action, AttendanceReviewRequest request, CancellationToken ct, bool created)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var r = await service.MutateAsync(employeeId, date, action, request, ct);
        return r.IsSuccess && created ? CreatedAtAction(nameof(Review), new { employeeId, date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) }, r.Value) : Result(r);
    }
    private ActionResult<T> Result<T>(ServiceResult<T> r) => r.IsSuccess ? Ok(r.Value) : r.Failure!.Code switch
    {
        "validation" => BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [r.Failure.Message] }) { Status = 400 }),
        "not_found" => NotFound(new ProblemDetails { Status = 404, Title = "Not found", Detail = r.Failure.Message }),
        "conflict" => Conflict(new ProblemDetails { Status = 409, Title = "Attendance requires review", Detail = r.Failure.Message }),
        _ => Problem(statusCode: 500)
    };
}
