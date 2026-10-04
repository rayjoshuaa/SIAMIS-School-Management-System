using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace SIAMIS.Api.Security;

public sealed class SecurityDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation,OperationFilterContext context)
    {
        if(context.ApiDescription.ActionDescriptor is ControllerActionDescriptor {ControllerName: "EmployeeDocuments" or "HrDocuments"} document)
        {
            var capability=context.ApiDescription.HttpMethod=="GET" ? "HRDocuments.Read" : "HRDocuments.Manage";
            operation.Description=(operation.Description ?? "")+" Requires "+capability+"; linked Leave evidence also requires Leave.Evidence. No Employee/SystemAdmin bypass.";
            if(document.ActionName=="Content" && operation.Responses?.TryGetValue("200",out var response)==true && response is OpenApiResponse content)
            {
                content.Content=new Dictionary<string,OpenApiMediaType>();
                foreach(var mime in new[]{"application/pdf","image/jpeg","image/png"})
                    content.Content[mime]=new OpenApiMediaType {Schema=new OpenApiSchema{Type=JsonSchemaType.String,Format="binary"}};
            }
        }
        if(context.ApiDescription.RelativePath?.StartsWith("api/auth/",StringComparison.Ordinal)==true)return;
        operation.Responses ??= new();
        operation.Responses.TryAdd("401",new OpenApiResponse{Description="Not authenticated or account/session no longer valid."});
        operation.Responses.TryAdd("403",new OpenApiResponse{Description="Authenticated but lacking capability or own-record authorization."});
    }
}
