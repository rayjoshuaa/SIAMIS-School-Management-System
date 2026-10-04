using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Security;
using SIAMIS.Infrastructure.Data;
using SIAMIS.Infrastructure.Security;

namespace SIAMIS.Api.Security;

public static class SecurityRegistration
{
    public static void AddSiamisSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentActor,CurrentActor>();
        builder.Services.AddScoped<IAccountService,AccountService>();
        builder.Services.AddScoped<SIAMIS.Application.Employees.IEmployeeAccountLifecycleService,EmployeeAccountLifecycleService>();
        builder.Services.AddScoped<IResourceAccessService,ResourceAccessService>();
        builder.Services.AddScoped<IHrSecurityReadService,HrSecurityReadService>();
        builder.Services.AddScoped<HrAuthorizationFilter>();
        builder.Services.AddScoped<ApiCsrfFilter>();
        builder.Services.AddScoped<PayrollResponseFilter>();
        builder.Services.AddScoped<LeaveEvidenceResponseFilter>();
        builder.Services.AddControllers(o=>{o.Filters.AddService<HrAuthorizationFilter>();o.Filters.AddService<ApiCsrfFilter>();o.Filters.AddService<PayrollResponseFilter>();o.Filters.AddService<LeaveEvidenceResponseFilter>();});
        builder.Services.AddIdentity<ApplicationUser,IdentityRole<Guid>>(o=>
        {
            o.Password.RequiredLength=12;o.Password.RequireDigit=false;o.Password.RequireLowercase=false;o.Password.RequireUppercase=false;o.Password.RequireNonAlphanumeric=false;
            o.Lockout.MaxFailedAccessAttempts=5;o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);
            o.User.RequireUniqueEmail=false;
        }).AddEntityFrameworkStores<SIAMISDbContext>().AddDefaultTokenProviders().AddClaimsPrincipalFactory<IdentityPrincipalFactory>();
        builder.Services.Configure<DataProtectionTokenProviderOptions>(o=>o.TokenLifespan=TimeSpan.FromMinutes(15));
        builder.Services.ConfigureApplicationCookie(o=>
        {
            o.Cookie.Name="SIAMIS.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
            o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
            o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
            o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
            o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
            o.Events.OnValidatePrincipal=async c=>
            {
                var users=c.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var u=await users.GetUserAsync(c.Principal!);
                var stamp=c.Principal!.FindFirstValue(users.Options.ClaimsIdentity.SecurityStampClaimType);
                if(u is null||!u.IsActive||await users.IsLockedOutAsync(u)||u.SecurityStamp!=stamp)
                {c.RejectPrincipal();return;}
                // Refresh account linkage and roles from authoritative server state on every request.
                c.ReplacePrincipal(await c.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>().CreateUserPrincipalAsync(u));
            };
        });
        builder.Services.AddAntiforgery(o=>
        {
            o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="SIAMIS.Csrf";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
            o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
        });
        builder.Services.AddAuthorization(o=>
        {
            o.FallbackPolicy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach(var capability in SecurityCapabilities.Roles.Values.SelectMany(x=>x).Append("MasterData.Read").Distinct())
                o.AddPolicy(capability,p=>p.RequireAuthenticatedUser().RequireAssertion(c=>!c.User.HasClaim("password_change","required")
                    &&SecurityCapabilities.ForRoles(c.User.FindAll(ClaimTypes.Role).Select(r=>r.Value)).Contains(capability)));
            o.AddPolicy("Unmapped", p=>p.RequireAssertion(_=>false));
        });
        builder.Services.AddRateLimiter(o=>
        {
            o.RejectionStatusCode=429;
            o.AddPolicy("login",http=>RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
        });
    }
}
