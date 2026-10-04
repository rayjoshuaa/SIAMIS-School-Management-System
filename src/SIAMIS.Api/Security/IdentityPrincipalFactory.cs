using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SIAMIS.Infrastructure.Security;

namespace SIAMIS.Api.Security;

public sealed class IdentityPrincipalFactory(UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser,IdentityRole<Guid>>(users,roles,options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity=await base.GenerateClaimsAsync(user);
        if(user.EmployeeId.HasValue)identity.AddClaim(new("employee_id",user.EmployeeId.Value.ToString()));
        if(user.RequiresPasswordChange)identity.AddClaim(new("password_change","required"));
        return identity;
    }
}
