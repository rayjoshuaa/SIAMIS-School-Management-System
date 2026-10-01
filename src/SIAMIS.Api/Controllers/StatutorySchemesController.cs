using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Maintains Thailand statutory scheme identities; no contribution or tax calculation.</summary>
/// <remarks>Production policy administration requires future authentication/RBAC. No actor identities are invented here.</remarks>
[Route("api/statutory-schemes")]
public sealed class StatutorySchemesController(IStatutoryPolicyService service, IStatutoryPolicyResolver resolver) : StatutoryConfigurationController
{
    /// <summary>Lists active schemes in Code order; includeInactive includes historical inactive schemes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StatutorySchemeDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<StatutorySchemeDto>>> List([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await service.GetSchemesAsync(includeInactive, ct));
    /// <summary>Returns a scheme, including inactive schemes.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StatutorySchemeDto), 200)]
    public async Task<ActionResult<StatutorySchemeDto>> Get(Guid id, CancellationToken ct)
    {
        var value = await service.GetSchemeAsync(id, ct);
        return value is null ? NotFound() : Ok(value);
    }
    /// <summary>Creates a SocialSecurity or PersonalIncomeTax scheme; Jurisdiction canonicalizes to TH.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(StatutorySchemeDto), 201)]
    public async Task<ActionResult<StatutorySchemeDto>> Create(StatutorySchemeRequest request, CancellationToken ct)
    {
        var result = await service.CreateSchemeAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.StatutorySchemeId }, result.Value) : Failure<StatutorySchemeDto>(result.Failure!);
    }
    /// <summary>Updates Name/IsActive. Code/type/jurisdiction are stable; published scheme names are immutable.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(StatutorySchemeDto), 200)]
    public async Task<ActionResult<StatutorySchemeDto>> Update(Guid id, StatutorySchemeRequest request, CancellationToken ct)
        => Respond(await service.UpdateSchemeAsync(id, request, ct));
    /// <summary>Deletes only a scheme with no policy versions or other references.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteSchemeAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }
    /// <summary>Resolves Published coverage using the caller's required governing date. No statutory governing-date rule or fallback is selected.</summary>
    /// <remarks>Inactive schemes remain resolvable for historical inspection. Outcomes: Resolved, NoApplicablePolicy, SchemeNotFound, Ambiguous.</remarks>
    [HttpGet("{id:guid}/resolve")]
    [ProducesResponseType(typeof(StatutoryPolicyResolution), 200)]
    [ProducesResponseType(typeof(StatutoryPolicyResolution), 404)]
    [ProducesResponseType(typeof(StatutoryPolicyResolution), 409)]
    public async Task<ActionResult<StatutoryPolicyResolution>> Resolve(Guid id, [FromQuery, System.ComponentModel.DataAnnotations.Required] DateOnly? governingDate, CancellationToken ct)
    {
        var result = await resolver.ResolveAsync(id, governingDate!.Value, ct);
        return StatusCode(result.Outcome == "Ambiguous" ? 409 : result.Outcome == "SchemeNotFound" ? 404 : 200, result);
    }
}
