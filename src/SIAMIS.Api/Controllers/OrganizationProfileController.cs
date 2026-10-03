using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Configures the single employer identity. Administrative authorization will be added with authentication.</summary>
[ApiController]
[Route("api/organization-profile")]
[Produces("application/json")]
public sealed class OrganizationProfileController(IOrganizationProfileService service) : ControllerBase
{
    /// <summary>Returns the configured employer; 404 means no profile is configured.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrganizationProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationProfileDto>> Get(CancellationToken ct)
    {
        var profile = await service.GetAsync(ct);
        return profile is null ? Problem(statusCode: 404, title: "Not found", detail: "Employer organization profile is not configured.") : Ok(profile);
    }

    /// <summary>Creates or replaces singleton employer configuration. Existing payslip identity remains frozen.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(OrganizationProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrganizationProfileDto>> Put([FromBody] OrganizationProfileRequest request, CancellationToken ct)
        => Ok(await service.PutAsync(request, ct));
}
