using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Security;

namespace SIAMIS.Api.Controllers;

[ApiController,Route("api/hr"),Produces("application/json")]
public sealed class HrOverviewController(IHrSecurityReadService reads) : ControllerBase
{
    /// <summary>Management/HR directory overview; excludes HR private fields, salary and bank data.</summary>
    [HttpGet("staff-overview")]
    public async Task<IActionResult> Staff(int page=1,int pageSize=20,CancellationToken ct=default)
        => Valid(page,pageSize)?Ok(await reads.StaffAsync(page,pageSize,ct)):BadRequest();
    /// <summary>Management Leave status overview; excludes reasons, policy/evidence and medical metadata.</summary>
    [HttpGet("leave-status")]
    public async Task<IActionResult> Leave(int page=1,int pageSize=20,CancellationToken ct=default)
        => Valid(page,pageSize)?Ok(await reads.LeaveStatusAsync(page,pageSize,ct)):BadRequest();
    internal static bool Valid(int page,int size)=>page>0&&size is >0 and <=100&&(long)(page-1)*size<=int.MaxValue;
}
