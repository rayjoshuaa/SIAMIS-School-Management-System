using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Reads and maintains employee document metadata for the 201 file.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/documents")]
[Produces("application/json")]
public sealed class EmployeeDocumentsController(IEmployeeContractDocumentService service) : ControllerBase
{
    /// <summary>Returns the employee's document metadata. This endpoint does not access stored files.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDocumentDto>>> GetDocuments(Guid employeeId, CancellationToken ct)
    {
        var result = await service.GetDocumentsAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeDocumentDto>>(result.Failure!);
    }

    /// <summary>Returns one employee document's metadata.</summary>
    [HttpGet("{documentId:guid}")]
    [ProducesResponseType(typeof(EmployeeDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDocumentDto>> GetDocument(Guid employeeId, Guid documentId, CancellationToken ct)
    {
        var result = await service.GetDocumentAsync(employeeId, documentId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeDocumentDto>(result.Failure!);
    }

    /// <summary>Creates document metadata only; no file is uploaded or written.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDocumentDto>> CreateDocument(Guid employeeId, [FromBody] EmployeeDocumentRequest request, CancellationToken ct)
    {
        var result = await service.CreateDocumentAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeeDocumentDto>(result.Failure!);
        return CreatedAtAction(nameof(GetDocument), new { employeeId, documentId = result.Value!.EmployeeDocumentId }, result.Value);
    }

    /// <summary>Replaces document metadata only; no file is uploaded or written.</summary>
    [HttpPut("{documentId:guid}")]
    [ProducesResponseType(typeof(EmployeeDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDocumentDto>> UpdateDocument(Guid employeeId, Guid documentId, [FromBody] EmployeeDocumentRequest request, CancellationToken ct)
    {
        var result = await service.UpdateDocumentAsync(employeeId, documentId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeDocumentDto>(result.Failure!);
    }

    /// <summary>Deletes document metadata. A contract link must be removed first.</summary>
    [HttpDelete("{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDocument(Guid employeeId, Guid documentId, CancellationToken ct)
    {
        var result = await service.DeleteDocumentAsync(employeeId, documentId, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    private ActionResult<T> Failure<T>(ApiFailure f) => f.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [f.Message] }) { Status = 400 })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = f.Message, Status = 404 })),
        "conflict" => new(StatusCode(409, new ProblemDetails { Title = "Conflict", Detail = f.Message, Status = 409 })),
        _ => new(Problem(statusCode: 500, title: "Unexpected error"))
    };
}
