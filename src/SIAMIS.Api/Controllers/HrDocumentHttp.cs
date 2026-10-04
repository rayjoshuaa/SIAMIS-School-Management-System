using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Private document failures never expose storage/database exception details to API clients.</summary>
public sealed class HrDocumentExceptionFilter(ILogger<HrDocumentExceptionFilter> log,IPrivateDocumentStorage storage) : IExceptionFilter,IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        // Authorization runs first; reject unavailable storage before any multipart buffering.
        if(context.ActionDescriptor is ControllerActionDescriptor {ActionName: "CreateDocument" or "Replace"} && !storage.IsConfigured)
            context.Result=new ObjectResult(new ProblemDetails{Status=503,Title="Document unavailable",Detail="Secure document storage is unavailable."}){StatusCode=503};
    }
    public void OnResourceExecuted(ResourceExecutedContext context) { }
    public void OnException(ExceptionContext context)
    {
        var status=context.Exception is BadHttpRequestException {StatusCode:413} ? 413 : 503;
        log.LogWarning("Private document request failed ({ExceptionType}).",context.Exception.GetType().Name);
        context.Result=new ObjectResult(new ProblemDetails{Status=status,Title="Document unavailable",Detail=status==413 ? "Maximum file size is 20 MiB." : "The private document operation could not be completed safely."}){StatusCode=status};
        context.ExceptionHandled=true;
    }
}

public sealed class HrDocumentUploadForm
{
    [Required] public IFormFile File { get; set; } = null!;
    public string Category { get; set; } = "GeneralHRDocument";
    public Guid? DocumentTypeId { get; set; }
    public Guid? EmploymentRecordId { get; set; }
    public Guid? LeaveId { get; set; }
    public Guid? LeaveEvidenceId { get; set; }
    [StringLength(2000)] public string? Remarks { get; set; }
    [StringLength(100)] public string? DocumentNumber { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public HrDocumentInput Input() => new(Category,DocumentTypeId,EmploymentRecordId,LeaveId,LeaveEvidenceId,Remarks,DocumentNumber,IssueDate,ExpiryDate);
}
public sealed class HrDocumentReplacementForm
{
    [Required] public IFormFile File { get; set; } = null!;
    [Required, StringLength(36,MinimumLength=36)] public string Version { get; set; } = "";
}
internal static class HrDocumentHttp
{
    public const long MaximumFileBytes=20L*1024*1024;
    public const long MaximumRequestBytes=MaximumFileBytes+65536;
    public static bool ValidForm(IFormCollection form, bool replacement)
    {
        string[] allowed=replacement ? ["version"] : ["category","documentTypeId","employmentRecordId","leaveId","leaveEvidenceId","remarks","documentNumber","issueDate","expiryDate"];
        return form.Files.Count==1 && string.Equals(form.Files[0].Name,"file",StringComparison.OrdinalIgnoreCase)
            && form.Keys.All(k=>allowed.Contains(k,StringComparer.OrdinalIgnoreCase) && form[k].Count==1);
    }
    public static IActionResult Failure(ControllerBase controller, ApiFailure f)
    {
        int status=f.Code switch { "validation"=>400,"forbidden"=>403,"not_found"=>404,"conflict"=>409,"too_large"=>413,"unsupported_type"=>415,"storage_unavailable"=>503,_=>500 };
        return controller.StatusCode(status,new ProblemDetails { Status=status,Title=status==503 ? "Document unavailable" : "Document request failed",Detail=f.Message });
    }
    public static IActionResult InvalidForm(ControllerBase c)=>Failure(c,new ApiFailure("validation","Exactly one file and only the documented form fields are permitted."));
    public static IActionResult TooLarge(ControllerBase c)=>Failure(c,new ApiFailure("too_large","Maximum file size is 20 MiB."));
    public static IActionResult Download(ControllerBase c,PrivateDocumentContent content)
    {
        c.Response.Headers.CacheControl="no-store";c.Response.Headers["X-Content-Type-Options"]="nosniff";
        return c.File(content.Stream,content.ContentType,content.FileName);
    }
}
