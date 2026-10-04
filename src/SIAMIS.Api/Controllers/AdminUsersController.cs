using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Security;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

[ApiController, Route("api/admin/users"), Produces("application/json")]
public sealed class AdminUsersController(IAccountService accounts) : ControllerBase
{
    /// <summary>Security administrator account list; never includes password hashes or security stamps.</summary>
    [HttpGet, ProducesResponseType(typeof(PagedResult<SecurityUserDto>),200), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> List([FromQuery] int page=1, [FromQuery] int pageSize=20, CancellationToken ct=default)
        => page<1 || pageSize is <1 or >100 || (long)(page-1)*pageSize>int.MaxValue ? BadRequest() : Ok(await accounts.ListAsync(page,pageSize,ct));
    /// <summary>Security administrator safe account detail.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) => Result(await accounts.GetAsync(id));
    /// <summary>Provision an account with a temporary password and explicit system roles; no anonymous registration. Requires password change before HR use.</summary>
    [HttpPost]
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
    private IActionResult Result(ServiceResult<SecurityUserDto> r)=>r.IsSuccess ? Ok(r.Value) : StatusCode(r.Failure!.Code switch {"not_found"=>404,"conflict"=>409,_=>400},new ProblemDetails{Title=r.Failure.Message});
}
