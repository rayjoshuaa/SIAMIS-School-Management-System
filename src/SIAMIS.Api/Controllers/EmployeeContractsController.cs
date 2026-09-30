using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Reads and maintains contract metadata for employees.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/contracts")]
[Produces("application/json")]
public sealed class EmployeeContractsController(IEmployeeContractDocumentService service) : ControllerBase
{
    /// <summary>Returns all contracts belonging to the employee.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeContractDto>>> GetContracts(Guid employeeId, CancellationToken ct)
    {
        var result = await service.GetContractsAsync(employeeId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeContractDto>>(result.Failure!);
    }

    /// <summary>Returns a contract for the specified employee.</summary>
    [HttpGet("{contractId:guid}")]
    [ProducesResponseType(typeof(EmployeeContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeContractDto>> GetContract(Guid employeeId, Guid contractId, CancellationToken ct)
    {
        var result = await service.GetContractAsync(employeeId, contractId, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeContractDto>(result.Failure!);
    }

    /// <summary>Creates contract metadata. Contract numbers must be unique.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeContractDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeContractDto>> CreateContract(Guid employeeId, [FromBody] EmployeeContractRequest request, CancellationToken ct)
    {
        var result = await service.CreateContractAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EmployeeContractDto>(result.Failure!);
        return CreatedAtAction(nameof(GetContract), new { employeeId, contractId = result.Value!.EmployeeContractId }, result.Value);
    }

    /// <summary>Replaces contract metadata while keeping employee ownership fixed.</summary>
    [HttpPut("{contractId:guid}")]
    [ProducesResponseType(typeof(EmployeeContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeContractDto>> UpdateContract(Guid employeeId, Guid contractId, [FromBody] EmployeeContractRequest request, CancellationToken ct)
    {
        var result = await service.UpdateContractAsync(employeeId, contractId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeContractDto>(result.Failure!);
    }

    /// <summary>Deletes a contract. Linked documents remain untouched.</summary>
    [HttpDelete("{contractId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteContract(Guid employeeId, Guid contractId, CancellationToken ct)
    {
        var result = await service.DeleteContractAsync(employeeId, contractId, ct);
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
