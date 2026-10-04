using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;

namespace SIAMIS.Api.Security;

public static class DeploymentSecurity
{
    public static void ConfigureDeploymentSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddDataProtection().SetApplicationName("SIAMIS");
        var keyDirectory=builder.Configuration["Security:DataProtectionKeyDirectory"];
        if(!string.IsNullOrWhiteSpace(keyDirectory))builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyDirectory)).SetApplicationName("SIAMIS");
        var proxies=builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>()??[];
        builder.Services.Configure<ForwardedHeadersOptions>(o=>
        {
            o.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;o.ForwardLimit=1;
            o.KnownIPNetworks.Clear();o.KnownProxies.Clear();
            foreach(var proxy in proxies)o.KnownProxies.Add(IPAddress.Parse(proxy));
            // Empty trust configuration must not enable blanket trust; middleware is not enabled without configured proxies.
        });
        var origins=builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()??[];
        if(origins.Any(origin=>!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.AbsolutePath!="/"||!builder.Environment.IsDevelopment()&&uri.Scheme!="https"))
            throw new InvalidOperationException("AllowedOrigins must be explicit origins; production requires HTTPS.");
        builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>
        {
            if(origins.Length==0)p.SetIsOriginAllowed(_=>false);else p.WithOrigins(origins);
            p.AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        }));
    }
    public static void UseDeploymentSecurity(this WebApplication app)
    {
        if(app.Configuration.GetSection("Security:KnownProxies").Get<string[]>() is {Length:>0})app.UseForwardedHeaders();
        if(!app.Environment.IsDevelopment())app.Use(async (context,next)=>
        {
            if(!context.Request.IsHttps){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{title="HTTPS is required",status=400});return;}
            await next(context);
        });
        app.UseCors();
    }
}
