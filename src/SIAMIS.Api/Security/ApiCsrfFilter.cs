using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SIAMIS.Api.Security;

/// <summary>Framework antiforgery validation without adding MVC view services to the API.</summary>
public sealed class ApiCsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order=>1000;
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        string method=context.HttpContext.Request.Method;
        if(HttpMethods.IsGet(method)||HttpMethods.IsHead(method)||HttpMethods.IsOptions(method)||HttpMethods.IsTrace(method))return;
        try{await antiforgery.ValidateRequestAsync(context.HttpContext);}
        catch(AntiforgeryValidationException)
        {
            context.Result=new BadRequestObjectResult(new ProblemDetails{Status=400,Title="Invalid or missing CSRF token"});
        }
    }
}
