using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Security;

namespace SIAMIS.Api.Controllers;

[ApiController,Route("api/self"),Produces("application/json")]
public sealed class SelfServiceController(IHrSecurityReadService reads,ICurrentActor actor) : ControllerBase
{
    /// <summary>Minimal own profile from authenticated User-to-Employee linkage; no client-selected employee ID.</summary>
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)=>actor.EmployeeId is Guid id?Ok(await reads.ProfileAsync(id,ct)):Forbid();
    /// <summary>Own Approved/Paid payrolls only. Ownership derives from authenticated linkage and is not a query parameter.</summary>
    [HttpGet("payrolls")]
    public async Task<IActionResult> Payrolls(int page=1,int pageSize=20,CancellationToken ct=default)
        => actor.EmployeeId is not Guid id?Forbid():!HrOverviewController.Valid(page,pageSize)?BadRequest():Ok(await reads.OwnPayrollsAsync(id,page,pageSize,ct));
}
