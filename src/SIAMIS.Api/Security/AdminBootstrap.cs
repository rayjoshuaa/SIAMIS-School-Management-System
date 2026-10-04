using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

namespace SIAMIS.Api.Security;

public static class AdminBootstrap
{
    /// <summary>Explicit one-time command only; credentials are supplied through environment or user secrets, never source.</summary>
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope=services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<SIAMISDbContext>();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        string name=config["Bootstrap:UserName"]??"",password=config["Bootstrap:Password"]??"";
        if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(password))throw new InvalidOperationException("Bootstrap credentials must be supplied securely.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var role=await db.Roles.SingleAsync(r=>r.Name=="SystemAdmin");
        if(await db.UserRoles.AnyAsync(r=>r.RoleId==role.Id))throw new InvalidOperationException("A SystemAdmin already exists; bootstrap is disabled.");
        var user=new ApplicationUser{Id=Guid.NewGuid(),UserName=name,RequiresPasswordChange=true,LockoutEnabled=true};
        var result=await users.CreateAsync(user,password);
        if(!result.Succeeded)throw new InvalidOperationException("Bootstrap account creation failed; verify password policy and account uniqueness.");
        result=await users.AddToRoleAsync(user,"SystemAdmin");
        if(!result.Succeeded)throw new InvalidOperationException("Bootstrap role assignment failed.");
        db.Add(new SecurityAuditEvent{ActorUserId=user.Id,Operation="FirstAdminBootstrapped",ResourceType="User",ResourceId=user.Id.ToString()});
        await db.SaveChangesAsync();await tx.CommitAsync();
    }
}
