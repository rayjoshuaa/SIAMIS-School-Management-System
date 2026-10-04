using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Security;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

[ApiController, Route("api/admin/users"), Produces("application/json")]
public sealed class AdminUsersController(IAccountService accounts, ICredentialService credentials) : ControllerBase
{
    /// <summary>Security administrator account list; never includes password hashes or security stamps.</summary>
    [HttpGet, ProducesResponseType(typeof(PagedResult<SecurityUserDto>),200), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> List([FromQuery] int page=1, [FromQuery] int pageSize=20, CancellationToken ct=default)
        => page<1 || pageSize is <1 or >100 || (long)(page-1)*pageSize>int.MaxValue ? BadRequest() : Ok(await accounts.ListAsync(page,pageSize,ct));
    /// <summary>Security administrator safe account detail.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) => Result(await accounts.GetAsync(id));
    /// <summary>Provision a passwordless account with intended delivery email and explicit roles. Sends initial activation; no public registration or administrator-selected password.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SecurityUserDto),201), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409), ProducesResponseType(503)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        var r=await accounts.CreateAsync(request,ct);
        return r.IsSuccess ? CreatedAtAction(nameof(Get),new{id=r.Value!.UserId},r.Value) : Result(r);
    }
    /// <summary>Enable/disable an account and revoke existing sessions. Requires latest administration Version.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id,UserStatusRequest request,CancellationToken ct)=>Result(await accounts.StatusAsync(id,request,ct));
    /// <summary>Replace roles using the latest Version; roles never derive from employment positions.</summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> Roles(Guid id,UserRolesRequest request,CancellationToken ct)=>Result(await accounts.RolesAsync(id,request,ct));
    /// <summary>Reissue activation for credential-pending users, or initiate reset for activated verified-email users. Requires current Version. No production token is returned.</summary>
    [HttpPost("{id:guid}/issue-credentials")]
    [ProducesResponseType(typeof(CredentialIssuedDto),200), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409), ProducesResponseType(503)]
    public async Task<IActionResult> Issue(Guid id, IssueCredentialRequest request, CancellationToken ct)
    {
        var r=await credentials.IssueAsync(id,request,ct);return r.IsSuccess?Ok(r.Value):Failure(r.Failure!);
    }
    private IActionResult Result(ServiceResult<SecurityUserDto> r)=>r.IsSuccess ? Ok(r.Value) : Failure(r.Failure!);
    private IActionResult Failure(ApiFailure f)=>StatusCode(f.Code switch {"not_found"=>404,"conflict" or "last_usable_system_admin_required"=>409,"forbidden"=>403,"delivery_unavailable"=>503,_=>400},new ProblemDetails{Title=f.Message,Extensions={ ["code"]=f.Code }});
}
