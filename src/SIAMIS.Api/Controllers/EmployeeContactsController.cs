using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages an employee's work and personal contact methods.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/contacts")]
[Produces("application/json")]
public sealed class EmployeeContactsController(IEmployeeContactsService contacts) : ControllerBase
{
    /// <summary>Returns the employee's contacts, with the primary contact first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeContactDto>>> GetContacts(Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await contacts.GetContactsAsync(employeeId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeContactDto>>(result.Failure!);
    }

    /// <summary>Adds a contact. The first contact becomes primary automatically when none is requested.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeContactDto>> CreateContact(Guid employeeId, [FromBody] CreateEmployeeContactRequest request, CancellationToken cancellationToken)
    {
        var result = await contacts.CreateContactAsync(employeeId, request, cancellationToken);
        if (!result.IsSuccess) return Failure<EmployeeContactDto>(result.Failure!);
        return CreatedAtAction(nameof(GetContacts), new { employeeId }, result.Value);
    }

    /// <summary>Updates supplied contact values and manages primary-contact selection.</summary>
    [HttpPut("{contactId:guid}")]
    [ProducesResponseType(typeof(EmployeeContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EmployeeContactDto>> UpdateContact(Guid employeeId, Guid contactId, [FromBody] UpdateEmployeeContactRequest request, CancellationToken cancellationToken)
    {
        var result = await contacts.UpdateContactAsync(employeeId, contactId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeContactDto>(result.Failure!);
    }

    /// <summary>Deletes a contact and promotes another contact if the deleted contact was primary.</summary>
    [HttpDelete("{contactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteContact(Guid employeeId, Guid contactId, CancellationToken cancellationToken)
    {
        var result = await contacts.DeleteContactAsync(employeeId, contactId, cancellationToken);
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
