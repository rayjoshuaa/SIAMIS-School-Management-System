using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;
[ApiController, Route("api/employees/{employeeId:guid}/photo")]
public sealed class EmployeePhotosController(IEmployeePhotoService photos) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid employeeId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await photos.GetAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }
    [HttpGet("content")]
    public async Task<IActionResult> Content(Guid employeeId, [FromQuery] Guid version, CancellationToken ct)
    {
        var result = await photos.ReadAsync(employeeId, version, ct);
        if (!result.IsSuccess) return Failure(result.Failure!);
        Response.Headers.CacheControl = "no-store, private";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return File(result.Value!.Stream, result.Value.ContentType);
    }
    [HttpPut, RequestSizeLimit(5 * 1024 * 1024 + 65536), RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024 + 65536)]
    public async Task<IActionResult> Upload(Guid employeeId, [FromForm] EmployeePhotoUpload request, CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (form.Files.Count != 1 || form.Files[0].Name != "file" || form.Keys.Any(x => !x.Equals("expectedVersion", StringComparison.OrdinalIgnoreCase) || form[x].Count != 1))
            return BadRequest(new ProblemDetails { Title = "Provide exactly one photo and its expected version." });
        if (request.File is null) return BadRequest();
        if (request.File.Length > 5 * 1024 * 1024) return StatusCode(413, new ProblemDetails { Title = "Maximum photo size is 5 MiB." });
        await using var bytes = request.File.OpenReadStream();
        var result = await photos.UploadAsync(employeeId, request.ExpectedVersion, bytes, request.File.FileName, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }
    [HttpDelete]
    public async Task<IActionResult> Remove(Guid employeeId, [FromBody] EmployeePhotoRemove request, CancellationToken ct)
    {
        var result = await photos.RemoveAsync(employeeId, request.ExpectedVersion, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure(result.Failure!);
    }
    private ObjectResult Failure(ApiFailure failure)
    {
        int status = failure.Code switch { "forbidden" => 403, "not_found" => 404, "conflict" => 409, "too_large" => 413, "unsupported_type" => 415, "validation" or "dimensions" => 400, _ => 503 };
        return StatusCode(status, new ProblemDetails { Status = status, Title = "Employee photo request failed", Detail = failure.Message });
    }
}
public sealed class EmployeePhotoUpload
{
    public IFormFile? File { get; set; }
    public Guid? ExpectedVersion { get; set; }
}
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class EmployeePhotoRemove { public Guid ExpectedVersion { get; set; } }
