using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using SIAMIS.Application.Security;

namespace SIAMIS.Api.Security;

public sealed class CurrentActor(IHttpContextAccessor http) : ICurrentActor
{
    public Guid? UserId => Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public Guid? EmployeeId => Guid.TryParse(http.HttpContext?.User.FindFirstValue("employee_id"), out var id) ? id : null;
    public bool HasCapability(string capability) => http.HttpContext?.User is { } p && p.Identity?.IsAuthenticated == true
        && !p.HasClaim("password_change", "required") && SecurityCapabilities.ForRoles(p.FindAll(ClaimTypes.Role).Select(r => r.Value)).Contains(capability);
    public string Operation => http.HttpContext is { } h ? $"{h.Request.Method} {h.GetEndpoint()?.DisplayName}" : "System";
}

/// <summary>Default-deny capability enforcement; ownership is resolved by the server, never supplied identity headers.</summary>
public sealed class HrAuthorizationFilter(IAuthorizationService authorization, ICurrentActor actor, IResourceAccessService resources) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true) { context.Result = new UnauthorizedResult(); return; }
        var action = (ControllerActionDescriptor)context.ActionDescriptor;
        if (action.ControllerName == "Auth") return;
        bool read = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method);
        string name = action.ControllerName;
        string capability = name switch
        {
            "AdminUsers" or "DevelopmentCredentialDelivery" => "Security.Manage",
            "AttendanceReporting" => action.ActionName == "Queue" ? "Attendance.Read" : "Reporting.Read",
            "HrOverview" => "Reporting.Read",
            "SelfService" => "SelfService",
            "AttendanceReview" => read ? "Attendance.Read" : action.ActionName is "FinalizeDay" or "Reopen" or "Confirm" ? "Attendance.Finalize" : "Attendance.Manage",
            "AttendanceFoundation" or "AttendanceDays" or "EmployeeAttendance" => read ? "Attendance.Read" : "Attendance.Manage",
            "LeaveEvidenceSandwich" => "Leave.Evidence",
            "EmployeeLeave" => read ? "Leave.Read" : action.ActionName is "Approve" or "Reject" ? "Leave.Review" : "Leave.Manage",
            "LeaveOperations" => "Leave.Read",
            "LeaveFoundation" => read ? "Leave.Read" : "Leave.Manage",
            "MasterData" or "Status" => read ? "MasterData.Read" : "Unmapped",
            _ when name.Contains("Payroll", StringComparison.Ordinal) || name.Contains("Statutory", StringComparison.Ordinal) || name.Contains("Tax", StringComparison.Ordinal) || name.Contains("Pit", StringComparison.Ordinal) || name is "EmployeeCompensations" or "OrganizationProfile" => read ? "Payroll.Read" : "Payroll.Manage",
            "Employees" or "EmployeeContacts" or "EmployeeAddresses" or "EmployeeEmergencyContacts" or "EmployeeContracts" or "EmployeeDocuments" or "EmployeeHistory" or "EmployeePerformance" or "EmploymentLifecycle" or "EmploymentStatuses" => read ? "Employee.Read" : "Employee.Manage",
            _ => "Unmapped"
        };
        Guid? employee = null;
        if (Guid.TryParse(context.RouteData.Values["employeeId"]?.ToString(), out var routeEmployee)) employee = routeEmployee;
        bool self = actor.EmployeeId.HasValue && employee == actor.EmployeeId;
        bool selfCapability = (await authorization.AuthorizeAsync(http.User, "SelfService")).Succeeded;
        if (name == "EmployeeLeave" && action.ActionName is "Approve" or "Reject" && self)
        { context.Result = new ForbidResult(); return; }
        bool allowed = (await authorization.AuthorizeAsync(http.User, capability)).Succeeded;
        if (!allowed && selfCapability && self)
            allowed = name == "AttendanceReporting" && action.ActionName is "History" or "Summary"
                || name == "EmployeeLeave" && action.ActionName is "GetLeaves" or "GetLeave" or "CreateLeave" or "Cancel"
                || name == "LeaveOperations" && action.ActionName == "Balances";
        if (!allowed && selfCapability && actor.EmployeeId.HasValue && read && name is "EmployeePayrolls" or "PayrollOperations" or "EmployeePayrollPitResults")
        {
            var raw = context.RouteData.Values["payrollId"] ?? context.RouteData.Values["id"];
            if (Guid.TryParse(raw?.ToString(), out var payroll)) allowed = await resources.OwnFinalPayrollAsync(payroll, actor.EmployeeId!.Value, http.RequestAborted);
        }
        if (!allowed) context.Result = new ForbidResult();
    }
}
