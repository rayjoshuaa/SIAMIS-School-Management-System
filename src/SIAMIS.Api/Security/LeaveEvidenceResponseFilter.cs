using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Security;

/// <summary>External evidence receipts are available only through the confidential evidence capability.</summary>
public sealed class LeaveEvidenceResponseFilter(IAuthorizationService authorization) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult result
            && !(await authorization.AuthorizeAsync(context.HttpContext.User, "Leave.Evidence")).Succeeded)
        {
            if (result.Value is EmployeeLeaveDto detail) detail.Evidence = null;
            if (result.Value is IEnumerable<EmployeeLeaveDto> list)
                foreach (var item in list) item.Evidence = null;
            if (result.Value is PagedResult<LeaveHistoryItemDto> history)
                foreach (var item in history.Items) item.Evidence = null;
        }
        await next();
    }
}
