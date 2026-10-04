using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Confidential HR documents require HRDocuments.Read/Manage. Employee capabilities are insufficient.</summary>
[ApiController, Route("api/employees/{employeeId:guid}/documents")]
[TypeFilter(typeof(HrDocumentExceptionFilter))]
public sealed class EmployeeDocumentsController(IHrDocumentService service) : ControllerBase
{
    /// <summary>Lists current documents; includeHistory includes archived/superseded metadata.</summary>
    [HttpGet, ProducesResponseType(typeof(IReadOnlyList<HrDocumentDto>),200), ProducesResponseType(400), ProducesResponseType(404)]
    public async Task<IActionResult> GetDocuments(Guid employeeId,bool includeHistory=false,string? category=null,CancellationToken ct=default)
    {
        var r=await service.ListAsync(employeeId,includeHistory,category,ct);
        return r.IsSuccess ? Ok(r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Reads metadata scoped to the specified employee.</summary>
    [HttpGet("{documentId:guid}"), ProducesResponseType(typeof(HrDocumentDto),200), ProducesResponseType(404)]
    public async Task<IActionResult> GetDocument(Guid employeeId,Guid documentId,CancellationToken ct)
    {
        var r=await service.GetAsync(documentId,employeeId,ct);
        return r.IsSuccess ? Ok(r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Uploads a private PDF/JPEG/PNG, maximum 20 MiB. Actor, key and integrity metadata are server-generated.</summary>
    [HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(HrDocumentHttp.MaximumRequestBytes,Order=int.MinValue), RequestFormLimits(MultipartBodyLengthLimit=HrDocumentHttp.MaximumRequestBytes,MemoryBufferThreshold=(int)HrDocumentHttp.MaximumRequestBytes,Order=int.MinValue)]
    [ProducesResponseType(typeof(HrDocumentDto),201), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(413), ProducesResponseType(415), ProducesResponseType(503)]
    public async Task<IActionResult> CreateDocument(Guid employeeId,[FromForm] HrDocumentUploadForm form,CancellationToken ct)
    {
        if(!HrDocumentHttp.ValidForm(Request.Form,false)) return HrDocumentHttp.InvalidForm(this);
        if(form.File.Length>HrDocumentHttp.MaximumFileBytes) return HrDocumentHttp.TooLarge(this);
        await using var bytes=form.File.OpenReadStream();
        var r=await service.UploadAsync(employeeId,form.Input(),bytes,form.File.FileName,form.File.ContentType,ct);
        return r.IsSuccess ? CreatedAtAction(nameof(GetDocument),new{employeeId,documentId=r.Value!.EmployeeDocumentId},r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Legacy metadata PUT is retired: use /replace for immutable content.</summary>
    [HttpPut("{documentId:guid}"), ProducesResponseType(409), ProducesResponseType(404)]
    public async Task<IActionResult> UpdateDocument(Guid employeeId,Guid documentId,CancellationToken ct)
    {
        var r=await service.GetAsync(documentId,employeeId,ct);
        return r.IsSuccess ? Conflict(new ProblemDetails{Status=409,Title="Use document replacement",Detail="Stored document metadata/content is immutable; use the replace endpoint."}) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Legacy DELETE archives the owned document; binary/history are retained. Version is required.</summary>
    [HttpDelete("{documentId:guid}"), ProducesResponseType(204), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<IActionResult> DeleteDocument(Guid employeeId,Guid documentId,[FromBody] HrDocumentLifecycleRequest request,CancellationToken ct)
    {
        var r=await service.ArchiveAsync(documentId,employeeId,request.Version,ct);
        return r.IsSuccess ? NoContent() : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Downloads exact integrity-verified content, including retained history.</summary>
    [HttpGet("{documentId:guid}/content"), ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(404), ProducesResponseType(503)]
    public async Task<IActionResult> Content(Guid employeeId,Guid documentId,CancellationToken ct)
    {
        var r=await service.DownloadAsync(documentId,employeeId,ct);
        return r.IsSuccess ? HrDocumentHttp.Download(this,r.Value!) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Creates a new immutable version. Stale versions return 409; prior metadata/bytes remain.</summary>
    [HttpPost("{documentId:guid}/replace"), Consumes("multipart/form-data"), RequestSizeLimit(HrDocumentHttp.MaximumRequestBytes,Order=int.MinValue), RequestFormLimits(MultipartBodyLengthLimit=HrDocumentHttp.MaximumRequestBytes,MemoryBufferThreshold=(int)HrDocumentHttp.MaximumRequestBytes,Order=int.MinValue)]
    [ProducesResponseType(typeof(HrDocumentDto),201), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409), ProducesResponseType(413), ProducesResponseType(415), ProducesResponseType(503)]
    public async Task<IActionResult> Replace(Guid employeeId,Guid documentId,[FromForm] HrDocumentReplacementForm form,CancellationToken ct)
    {
        if(!HrDocumentHttp.ValidForm(Request.Form,true)) return HrDocumentHttp.InvalidForm(this);
        if(form.File.Length>HrDocumentHttp.MaximumFileBytes) return HrDocumentHttp.TooLarge(this);
        await using var bytes=form.File.OpenReadStream();
        var r=await service.ReplaceAsync(documentId,employeeId,form.Version,bytes,form.File.FileName,form.File.ContentType,ct);
        return r.IsSuccess ? CreatedAtAction(nameof(GetDocument),new{employeeId,documentId=r.Value!.EmployeeDocumentId},r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Archives without deleting historical associations or bytes.</summary>
    [HttpPost("{documentId:guid}/archive"), ProducesResponseType(typeof(HrDocumentDto),200), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<IActionResult> Archive(Guid employeeId,Guid documentId,[FromBody] HrDocumentLifecycleRequest request,CancellationToken ct)
    {
        var r=await service.ArchiveAsync(documentId,employeeId,request.Version,ct);
        return r.IsSuccess ? Ok(r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
}
