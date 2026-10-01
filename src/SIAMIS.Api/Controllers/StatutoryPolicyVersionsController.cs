using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Authors typed Draft policies and publishes immutable configuration contracts. No statutory calculation.</summary>
/// <remarks>Publication validates structure, not legal truth. Method IDs SSO-TH-V1/PIT-TH-V1 identify D4A data contracts; D5/D6 must separately verify runtime compatibility. Production requires future RBAC.</remarks>
[Route("api/statutory-policy-versions")]
public sealed class StatutoryPolicyVersionsController(IStatutoryPolicyService service) : StatutoryConfigurationController
{
    /// <summary>Lists Draft and Published versions with optional scheme filtering.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StatutoryPolicyDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<StatutoryPolicyDto>>> List([FromQuery] Guid? schemeId, CancellationToken ct)
        => Ok(await service.GetPoliciesAsync(schemeId, ct));
    /// <summary>Reads complete typed configuration, including immutable Published history.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StatutoryPolicyDto), 200)]
    public async Task<ActionResult<StatutoryPolicyDto>> Get(Guid id, CancellationToken ct)
    {
        var value = await service.GetPolicyAsync(id, ct);
        return value is null ? NotFound() : Ok(value);
    }
    /// <summary>Creates a Draft under an active scheme. Currency canonicalizes to THB; typed configuration may initially be incomplete.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(StatutoryPolicyDto), 201)]
    public async Task<ActionResult<StatutoryPolicyDto>> Create(StatutoryPolicyCreateRequest request, CancellationToken ct)
    {
        var result = await service.CreatePolicyAsync(request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value!.StatutoryPolicyVersionId }, result.Value) : Failure<StatutoryPolicyDto>(result.Failure!);
    }
    /// <summary>Replaces Draft metadata only. Status/publication timestamps/scheme reassignment are not request fields.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(StatutoryPolicyDto), 200)]
    public async Task<ActionResult<StatutoryPolicyDto>> Update(Guid id, StatutoryPolicyRequest request, CancellationToken ct)
        => Respond(await service.UpdatePolicyAsync(id, request, ct));
    /// <summary>Replaces a Draft Social Security configuration. Nullable parameters permit incomplete drafts; rates use percentage points. No contributions are computed.</summary>
    [HttpPut("{id:guid}/social-security")]
    [ProducesResponseType(typeof(StatutoryPolicyDto), 200)]
    public async Task<ActionResult<StatutoryPolicyDto>> SocialSecurity(Guid id, SocialSecurityPolicyRequest request, CancellationToken ct)
        => Respond(await service.SetSocialSecurityAsync(id, request, ct));
    /// <summary>Replaces Draft PIT configuration and brackets. Bounds are lower-inclusive/upper-exclusive; rates are percentage points. Draft gaps are allowed, overlaps are rejected.</summary>
    [HttpPut("{id:guid}/personal-income-tax")]
    [ProducesResponseType(typeof(StatutoryPolicyDto), 200)]
    public async Task<ActionResult<StatutoryPolicyDto>> Pit(Guid id, PitPolicyRequest request, CancellationToken ct)
        => Respond(await service.SetPitAsync(id, request, ct));
    /// <summary>Publishes a complete Draft once. Requires official-reference/method metadata; validates PIT zero-to-infinity coverage and serializes inclusive-date overlap checks. Never shortens a predecessor.</summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(StatutoryPolicyDto), 200)]
    public async Task<ActionResult<StatutoryPolicyDto>> Publish(Guid id, CancellationToken ct)
        => Respond(await service.PublishAsync(id, ct));
    /// <summary>Deletes only an unreferenced Draft and its owned configuration. Published versions are protected.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeletePolicyAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }
}
