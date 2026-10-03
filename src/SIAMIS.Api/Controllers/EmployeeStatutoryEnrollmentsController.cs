using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Explicit employee Social Security or TH-PIT applicability inputs; no inferred eligibility.</summary>
/// <remarks>Production requires authentication/RBAC. Membership identifiers are omitted from lists and resolution summaries.</remarks>
[Route("api/employees/{employeeId:guid}/statutory-enrollments")]
public sealed class EmployeeStatutoryEnrollmentsController(IEmployeeStatutoryService service) : StatutoryConfigurationController
{
    /// <summary>Lists immutable enrollment history, ordered by scheme and EffectiveFrom, without membership identifiers.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StatutoryEnrollmentSummaryDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<StatutoryEnrollmentSummaryDto>>> List(Guid employeeId, CancellationToken ct)
        => Respond(await service.ListEnrollmentsAsync(employeeId, ct));

    /// <summary>Reads one enrollment belonging to this employee, including its sensitive membership identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StatutoryEnrollmentDto), 200)]
    public async Task<ActionResult<StatutoryEnrollmentDto>> Get(Guid employeeId, Guid id, CancellationToken ct)
        => Respond(await service.GetEnrollmentAsync(employeeId, id, ct));

    /// <summary>Creates an explicit Applicable/NotApplicable interval. Inclusive dates may be adjacent but cannot overlap; no automatic prior-interval shortening.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(StatutoryEnrollmentDto), 201)]
    public async Task<ActionResult<StatutoryEnrollmentDto>> Create(Guid employeeId, StatutoryEnrollmentRequest request, CancellationToken ct)
    {
        var result = await service.CreateEnrollmentAsync(employeeId, request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { employeeId, id = result.Value!.EmployeeStatutoryEnrollmentId }, result.Value)
            : Failure<StatutoryEnrollmentDto>(result.Failure!);
    }

    /// <summary>Ends an open interval inclusively on today or a future date. Closed intervals and past coverage cannot be rewritten.</summary>
    [HttpPost("{id:guid}/end")]
    [ProducesResponseType(typeof(StatutoryEnrollmentDto), 200)]
    public async Task<ActionResult<StatutoryEnrollmentDto>> End(Guid employeeId, Guid id, StatutoryEnrollmentEndRequest request, CancellationToken ct)
        => Respond(await service.EndEnrollmentAsync(employeeId, id, request, ct));

    /// <summary>Resolves employee/scheme applicability on the required caller-supplied date. Absence returns Unknown; ambiguous coverage returns 409.</summary>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(StatutoryEnrollmentResolution), 200)]
    public async Task<ActionResult<StatutoryEnrollmentResolution>> Resolve(Guid employeeId,
        [FromQuery, Required] Guid? schemeId, [FromQuery, Required] DateOnly? date, CancellationToken ct)
        => Respond(await service.ResolveEnrollmentAsync(employeeId, schemeId!.Value, date!.Value, ct));
}
