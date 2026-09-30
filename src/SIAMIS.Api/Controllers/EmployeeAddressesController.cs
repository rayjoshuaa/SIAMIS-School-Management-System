using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages an employee's current and permanent addresses.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/addresses")]
[Produces("application/json")]
public sealed class EmployeeAddressesController(IEmployeeAddressesService addresses) : ControllerBase
{
    /// <summary>Returns the employee's addresses, with the primary address first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeAddressDto>>> GetAddresses(Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await addresses.GetAddressesAsync(employeeId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeAddressDto>>(result.Failure!);
    }

    /// <summary>Adds an employee address and makes it primary when it is the employee's first address.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeAddressDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeAddressDto>> CreateAddress(Guid employeeId, [FromBody] CreateEmployeeAddressRequest request, CancellationToken cancellationToken)
    {
        var result = await addresses.CreateAddressAsync(employeeId, request, cancellationToken);
        if (!result.IsSuccess) return Failure<EmployeeAddressDto>(result.Failure!);
        return CreatedAtAction(nameof(GetAddresses), new { employeeId }, result.Value);
    }

    /// <summary>Updates supplied address fields and maintains a single primary address.</summary>
    [HttpPut("{addressId:guid}")]
    [ProducesResponseType(typeof(EmployeeAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeAddressDto>> UpdateAddress(Guid employeeId, Guid addressId, [FromBody] UpdateEmployeeAddressRequest request, CancellationToken cancellationToken)
    {
        var result = await addresses.UpdateAddressAsync(employeeId, addressId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeAddressDto>(result.Failure!);
    }

    /// <summary>Deletes an address and promotes another address if the deleted address was primary.</summary>
    [HttpDelete("{addressId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(Guid employeeId, Guid addressId, CancellationToken cancellationToken)
    {
        var result = await addresses.DeleteAddressAsync(employeeId, addressId, cancellationToken);
        if (!result.IsSuccess) return Failure<bool>(result.Failure!).Result!;
        return NoContent();
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new ActionResult<T>(BadRequest(new ValidationProblemDetails(
            new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        "not_found" => new ActionResult<T>(NotFound(new ProblemDetails
        {
            Title = "Not found", Detail = failure.Message, Status = StatusCodes.Status404NotFound
        })),
        "conflict" => new ActionResult<T>(Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: failure.Message)),
        _ => new ActionResult<T>(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
