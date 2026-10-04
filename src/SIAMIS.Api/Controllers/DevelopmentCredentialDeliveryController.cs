using Microsoft.AspNetCore.Mvc;
using SIAMIS.Api.Security;
using SIAMIS.Application.Security;

namespace SIAMIS.Api.Controllers;

/// <summary>Explicit Development-only test delivery. Not an ordinary credential issuance response.</summary>
[ApiController,Route("api/admin/users/{id:guid}/credential-delivery"),Produces("application/json")]
public sealed class DevelopmentCredentialDeliveryController(IHostEnvironment environment, ICredentialDelivery delivery) : ControllerBase
{
    /// <summary>Collect one transient test delivery. Requires Security.Manage, Development and explicitly enabled test delivery. Never available in Production.</summary>
    [HttpPost,ProducesResponseType(typeof(CredentialDeliveryMessage),200),ProducesResponseType(404),ProducesResponseType(400)]
    public IActionResult Collect(Guid id)
    {
        Response.Headers.CacheControl="no-store";
        if(!environment.IsDevelopment() || delivery is not DevelopmentCredentialDelivery local)return NotFound();
        var message=local.Collect(id);return message is null?NotFound():Ok(message);
    }
}
