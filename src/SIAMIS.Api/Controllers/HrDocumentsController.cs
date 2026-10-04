using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Direct document-ID operations enforce the same HRDocuments capabilities and context validation.</summary>
[ApiController, Route("api/hr-documents")]
[TypeFilter(typeof(HrDocumentExceptionFilter))]
public sealed class HrDocumentsController(IHrDocumentService service) : ControllerBase
{
    /// <summary>Reads confidential document metadata by stable ID.</summary>
    [HttpGet("{documentId:guid}"), ProducesResponseType(typeof(HrDocumentDto),200), ProducesResponseType(404)]
    public async Task<IActionResult> GetDocument(Guid documentId,CancellationToken ct)
    {
        var r=await service.GetAsync(documentId,null,ct);
        return r.IsSuccess ? Ok(r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Downloads only after authorization and SHA-256/size verification.</summary>
    [HttpGet("{documentId:guid}/content"), ProducesResponseType(typeof(FileStreamResult),200), ProducesResponseType(404), ProducesResponseType(503)]
    public async Task<IActionResult> Content(Guid documentId,CancellationToken ct)
    {
        var r=await service.DownloadAsync(documentId,null,ct);
        return r.IsSuccess ? HrDocumentHttp.Download(this,r.Value!) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Creates a new immutable PDF/JPEG/PNG version, maximum 20 MiB. Requires current version.</summary>
    [HttpPost("{documentId:guid}/replace"), Consumes("multipart/form-data"), RequestSizeLimit(HrDocumentHttp.MaximumRequestBytes,Order=int.MinValue), RequestFormLimits(MultipartBodyLengthLimit=HrDocumentHttp.MaximumRequestBytes,MemoryBufferThreshold=(int)HrDocumentHttp.MaximumRequestBytes,Order=int.MinValue)]
    [ProducesResponseType(typeof(HrDocumentDto),201), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409), ProducesResponseType(413), ProducesResponseType(415), ProducesResponseType(503)]
    public async Task<IActionResult> Replace(Guid documentId,[FromForm] HrDocumentReplacementForm form,CancellationToken ct)
    {
        if(!HrDocumentHttp.ValidForm(Request.Form,true)) return HrDocumentHttp.InvalidForm(this);
        if(form.File.Length>HrDocumentHttp.MaximumFileBytes) return HrDocumentHttp.TooLarge(this);
        await using var bytes=form.File.OpenReadStream();
        var r=await service.ReplaceAsync(documentId,null,form.Version,bytes,form.File.FileName,form.File.ContentType,ct);
        return r.IsSuccess ? CreatedAtAction(nameof(GetDocument),new{documentId=r.Value!.EmployeeDocumentId},r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
    /// <summary>Archives current content, retaining historical bytes and links. Requires current version.</summary>
    [HttpPost("{documentId:guid}/archive"), ProducesResponseType(typeof(HrDocumentDto),200), ProducesResponseType(400), ProducesResponseType(404), ProducesResponseType(409)]
    public async Task<IActionResult> Archive(Guid documentId,[FromBody] HrDocumentLifecycleRequest request,CancellationToken ct)
    {
        var r=await service.ArchiveAsync(documentId,null,request.Version,ct);
        return r.IsSuccess ? Ok(r.Value) : HrDocumentHttp.Failure(this,r.Failure!);
    }
}
