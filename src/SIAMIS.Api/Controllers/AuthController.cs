using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SIAMIS.Application.Security;

namespace SIAMIS.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAccountService accounts, IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>Obtain a CSRF token; send it in X-CSRF-TOKEN for every unsafe API request, including login. Obtain a new token after login/logout.</summary>
    [AllowAnonymous, HttpGet("csrf")]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }
    /// <summary>Login using framework Identity; generic 401 for invalid/disabled/locked accounts. Requires CSRF token. No public registration.</summary>
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    [ProducesResponseType(204), ProducesResponseType(401), ProducesResponseType(400), ProducesResponseType(429)]
    public async Task<IActionResult> Login(LoginRequest request) => await accounts.LoginAsync(request) ? NoContent() : Unauthorized();
    /// <summary>Sign out the current browser session. Requires CSRF token.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(204), ProducesResponseType(401), ProducesResponseType(400)]
    public async Task<IActionResult> Logout() { await accounts.LogoutAsync(); return NoContent(); }
    /// <summary>Safe current identity and capabilities. Temporary-password accounts must change their password before HR access.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(SecurityUserDto), 200), ProducesResponseType(401)]
    public async Task<IActionResult> Me() { Response.Headers.CacheControl = "no-store"; return Ok(await accounts.MeAsync()); }
    /// <summary>Change password, revoke previous sessions and complete temporary-password activation. Requires current password and CSRF token.</summary>
    [HttpPost("change-password")]
    [ProducesResponseType(204), ProducesResponseType(400), ProducesResponseType(401)]
    public async Task<IActionResult> Password(ChangePasswordRequest request) => await accounts.ChangePasswordAsync(request) ? NoContent() : BadRequest(new ProblemDetails { Status = 400, Title = "Password change failed" });
}
