using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

internal static class D13CredentialTests
{
    public static void Run(Action<bool,string> check)
    {
        var pending=new ApplicationUser{Id=Guid.NewGuid(),UserName="Synthetic",Email="synthetic@example.invalid",SecurityStamp=Guid.NewGuid().ToString()};
        check(pending.PasswordHash is null && !pending.EmailConfirmed && pending.RequiresPasswordChange,"D13 passwordless pending uses existing Identity state");
        check(CredentialService.ActivationEligible(pending) && !CredentialService.RecoveryEligible(pending),"D13 pending invitation is not anonymous recovery");
        pending.IsActive=false;
        check(!CredentialService.ActivationEligible(pending) && !CredentialService.RecoveryEligible(pending),"D13 disabled cannot activate or recover");
        pending.IsActive=true;pending.PasswordHash="Synthetic marker";
        check(!CredentialService.RecoveryEligible(pending),"D13 unconfirmed email cannot recover");
        pending.EmailConfirmed=true;
        check(CredentialService.RecoveryEligible(pending),"D13 confirmed active established credential eligible");
        pending.Email=null;
        check(!CredentialService.RecoveryEligible(pending),"D13 no email no anonymous recovery");
        pending.Email="synthetic@example.invalid";pending.IsActive=false;
        check(!CredentialService.RecoveryEligible(pending),"D13 reset never bypasses disabled state");
        foreach(var name in new[]{"TemporaryPassword","Password","PasswordHash","SecurityStamp","EmailConfirmed","ActorUserId","Claims"})
            check(typeof(CreateUserRequest).GetProperty(name) is null,"D13 provisioning cannot select "+name);
        foreach(var type in new[]{typeof(ForgotPasswordRequest),typeof(CompleteCredentialRequest),typeof(IssueCredentialRequest)})
        {
            try{JsonSerializer.Deserialize("{\"ActorUserId\":\"forged\"}",type);check(false,"D13 strict credential request");}
            catch(JsonException){check(true,"D13 credential actor forgery rejected "+type.Name);}
        }
        var missingEmail=new CreateUserRequest{UserName="Synthetic"};
        check(!Validator.TryValidateObject(missingEmail,new(missingEmail),[],true),"D13 normal provisioning requires delivery email");
        using var db=new SIAMISDbContext(new DbContextOptionsBuilder<SIAMISDbContext>().UseSqlServer("Server=localhost;Database=PureModelOnly").Options);
        var store=new UserStore<ApplicationUser,IdentityRole<Guid>,SIAMISDbContext,Guid>(db);
        using var services=new ServiceCollection().BuildServiceProvider();
        using var manager=new UserManager<ApplicationUser>(store,Options.Create(new IdentityOptions()),new PasswordHasher<ApplicationUser>(),[],[],new UpperInvariantLookupNormalizer(),new IdentityErrorDescriber(),services,NullLogger<UserManager<ApplicationUser>>.Instance);
        var protection=new EphemeralDataProtectionProvider();
        var tokenOptions=new DataProtectionTokenProviderOptions{TokenLifespan=TimeSpan.FromMinutes(15)};
        var provider=new DataProtectorTokenProvider<ApplicationUser>(protection,Options.Create(tokenOptions),NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance);
        pending.IsActive=true;pending.PasswordHash=null;pending.EmailConfirmed=false;
        string purpose=CredentialService.ActivationPurpose;
        var token=provider.GenerateAsync(purpose,manager,pending).GetAwaiter().GetResult();
        bool Valid(string value,string p,ApplicationUser u)=>provider.ValidateAsync(p,value,manager,u).GetAwaiter().GetResult();
        check(Valid(token,purpose,pending),"D13 framework activation token valid");
        check(!Valid(token,"ResetPassword",pending),"D13 activation purpose cannot reset established password");
        check(!Valid(token,purpose,new ApplicationUser{Id=Guid.NewGuid(),SecurityStamp=pending.SecurityStamp}),"D13 token cannot target another user");
        check(!Valid("malformed",purpose,pending),"D13 malformed framework token rejected");
        pending.SecurityStamp=Guid.NewGuid().ToString();
        check(!Valid(token,purpose,pending),"D13 successful consumption/reissue stamp makes old token stale");
        var expired=new DataProtectorTokenProvider<ApplicationUser>(protection,Options.Create(new DataProtectionTokenProviderOptions{TokenLifespan=TimeSpan.Zero}),NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance);
        var old=expired.GenerateAsync(purpose,manager,pending).GetAwaiter().GetResult();Task.Delay(10).GetAwaiter().GetResult();
        check(!expired.ValidateAsync(purpose,old,manager,pending).GetAwaiter().GetResult(),"D13 framework expiry enforced without production lifetime override");
        var expiredReset=expired.GenerateAsync("ResetPassword",manager,pending).GetAwaiter().GetResult();Task.Delay(10).GetAwaiter().GetResult();
        check(!expired.ValidateAsync("ResetPassword",expiredReset,manager,pending).GetAwaiter().GetResult(),"D13 reset purpose also enforces framework expiry");
        check(tokenOptions.TokenLifespan==TimeSpan.FromMinutes(15),"D13 actual 15-minute contract preserved");
    }
}
