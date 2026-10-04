using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SIAMIS.Application.Security;

namespace SIAMIS.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAccountService accounts, ICredentialService credentials, IAntiforgery antiforgery) : ControllerBase
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
    /// <summary>Generic verified-email recovery request. Identical response for unknown, unconfirmed, pending, disabled and eligible accounts. Requires CSRF.</summary>
    [AllowAnonymous,HttpPost("forgot-password"),EnableRateLimiting("credential")]
    [ProducesResponseType(200),ProducesResponseType(400),ProducesResponseType(429)]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request,CancellationToken ct)
    {
        await credentials.ForgotAsync(request,ct);Response.Headers.CacheControl="no-store";
        return Ok(new{message="If an eligible account exists, password reset instructions have been issued."});
    }
    /// <summary>Establish password and verify the provisioned delivery email using a valid activation token. Never enables a disabled account. Requires CSRF.</summary>
    [AllowAnonymous,HttpPost("activate"),EnableRateLimiting("credential")]
    [ProducesResponseType(204),ProducesResponseType(400),ProducesResponseType(429)]
    public async Task<IActionResult> Activate(CompleteCredentialRequest request,CancellationToken ct)=>await credentials.CompleteAsync(request,true,ct)?NoContent():CredentialFailure();
    /// <summary>Replace password using a valid Identity reset token; revokes prior sessions without changing administrative state, linkage, roles or employment. Requires CSRF.</summary>
    [AllowAnonymous,HttpPost("reset-password"),EnableRateLimiting("credential")]
    [ProducesResponseType(204),ProducesResponseType(400),ProducesResponseType(429)]
    public async Task<IActionResult> Reset(CompleteCredentialRequest request,CancellationToken ct)=>await credentials.CompleteAsync(request,false,ct)?NoContent():CredentialFailure();
    private IActionResult CredentialFailure()=>BadRequest(new ProblemDetails{Status=400,Title="Credential operation failed"});
}
