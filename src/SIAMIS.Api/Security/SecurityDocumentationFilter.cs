using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SIAMIS.Api.Security;

public sealed class SecurityDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation,OperationFilterContext context)
    {
        if(context.ApiDescription.RelativePath?.StartsWith("api/auth/",StringComparison.Ordinal)==true)return;
        operation.Responses ??= new();
        operation.Responses.TryAdd("401",new OpenApiResponse{Description="Not authenticated or account/session no longer valid."});
        operation.Responses.TryAdd("403",new OpenApiResponse{Description="Authenticated but lacking capability or own-record authorization."});
    }
}
