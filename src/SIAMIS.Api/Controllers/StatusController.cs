using Microsoft.AspNetCore.Mvc;

namespace SIAMIS.Api.Controllers;

[ApiController]
[Route("api/status")]
public sealed class StatusController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(StatusResponse), StatusCodes.Status200OK)]
    public ActionResult<StatusResponse> Get() => Ok(new StatusResponse(
        "Operational",
        "SIAMIS API",
        DateTimeOffset.UtcNow));

    public sealed record StatusResponse(string Status, string Service, DateTimeOffset TimestampUtc);
}
