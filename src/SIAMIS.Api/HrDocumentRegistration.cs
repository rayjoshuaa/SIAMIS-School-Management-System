using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Documents;

namespace SIAMIS.Api;

internal static class HrDocumentRegistration
{
    public static void AddPrivateHrDocuments(this WebApplicationBuilder builder)
    {
        var configured=builder.Configuration["PrivateDocuments:Root"];
        // Development-only private default. Production never inherits it.
        var root=string.IsNullOrWhiteSpace(configured) ? builder.Environment.IsDevelopment()
            ? Path.Combine(builder.Environment.ContentRootPath,"App_Data","hr-documents") : null : configured;
        if(root is not null)
        {
            if(!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("PrivateDocuments:Root must be an absolute private directory.");
            var full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            var web=Path.GetFullPath(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath,"wwwroot"))
                .TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(Within(full,web) || Within(web,full)) throw new InvalidOperationException("Private document storage must be isolated from the public web root.");
            root=full;
        }
        builder.Services.Configure<PrivateDocumentOptions>(o=>o.Root=root);
        builder.Services.AddSingleton<IPrivateDocumentStorage,PrivateDocumentStorage>();
        builder.Services.AddScoped<IHrDocumentService,HrDocumentService>();
    }
    private static bool Within(string path,string parent)=>path.Equals(parent,StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
}
