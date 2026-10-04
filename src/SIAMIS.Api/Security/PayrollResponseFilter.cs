using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Security;

/// <summary>Employee administration does not implicitly grant access to compensation.</summary>
public sealed class PayrollResponseFilter(IAuthorizationService authorization) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: EmployeeDetailDto employee }
            && !(await authorization.AuthorizeAsync(context.HttpContext.User, "Payroll.Read")).Succeeded)
            employee.CurrentCompensation = null;
        await next();
    }
}
