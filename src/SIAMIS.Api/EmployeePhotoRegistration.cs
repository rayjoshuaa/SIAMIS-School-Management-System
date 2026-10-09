using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Documents;

namespace SIAMIS.Api;
internal static class EmployeePhotoRegistration
{
    public static void AddEmployeePhotos(this WebApplicationBuilder builder)
    {
        var configured = builder.Configuration["EmployeePhotos:Root"];
        var root = string.IsNullOrWhiteSpace(configured) ? builder.Environment.IsDevelopment()
            ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "employee-photos") : null : configured;
        if (root is not null)
        {
            if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("EmployeePhotos:Root must be an absolute private directory.");
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var web = Path.GetFullPath(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot")).TrimEnd(Path.DirectorySeparatorChar);
            if (Within(root, web) || Within(web, root)) throw new InvalidOperationException("Employee photo storage must be outside the web root.");
            var documents = builder.Configuration["PrivateDocuments:Root"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "hr-documents");
            documents = Path.GetFullPath(documents).TrimEnd(Path.DirectorySeparatorChar);
            if (Within(root, documents) || Within(documents, root)) throw new InvalidOperationException("Employee photos need a separate private storage directory.");
        }
        builder.Services.Configure<EmployeePhotoStorageOptions>(o => o.Root = root);
        builder.Services.AddSingleton<IEmployeePhotoStorage, EmployeePhotoStorage>();
        builder.Services.AddScoped<IEmployeePhotoService, EmployeePhotoService>();
    }
    private static bool Within(string path, string parent) => path.Equals(parent, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
