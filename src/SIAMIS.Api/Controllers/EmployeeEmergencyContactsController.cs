using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages emergency contacts for an employee.</summary>
[ApiController]
[Route("api/employees/{employeeId:guid}/emergency-contacts")]
[Produces("application/json")]
public sealed class EmployeeEmergencyContactsController(IEmployeeEmergencyContactsService emergencyContacts) : ControllerBase
{
    /// <summary>Returns an employee's emergency contacts, with the primary contact first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeEmergencyContactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeEmergencyContactDto>>> GetEmergencyContacts(Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await emergencyContacts.GetEmergencyContactsAsync(employeeId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<EmployeeEmergencyContactDto>>(result.Failure!);
    }

    /// <summary>Adds an emergency contact. The first emergency contact is made primary automatically.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> CreateEmergencyContact(Guid employeeId, [FromBody] CreateEmergencyContactRequest request, CancellationToken cancellationToken)
    {
        var result = await emergencyContacts.CreateEmergencyContactAsync(employeeId, request, cancellationToken);
        if (!result.IsSuccess) return Failure<EmployeeEmergencyContactDto>(result.Failure!);
        return CreatedAtAction(nameof(GetEmergencyContacts), new { employeeId }, result.Value);
    }

    /// <summary>Updates supplied emergency-contact fields and maintains a single primary contact.</summary>
    [HttpPut("{emergencyContactId:guid}")]
    [ProducesResponseType(typeof(EmployeeEmergencyContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeEmergencyContactDto>> UpdateEmergencyContact(Guid employeeId, Guid emergencyContactId, [FromBody] UpdateEmergencyContactRequest request, CancellationToken cancellationToken)
    {
        var result = await emergencyContacts.UpdateEmergencyContactAsync(employeeId, emergencyContactId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Failure<EmployeeEmergencyContactDto>(result.Failure!);
    }

    /// <summary>Deletes an emergency contact and promotes another if the deleted contact was primary.</summary>
    [HttpDelete("{emergencyContactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmergencyContact(Guid employeeId, Guid emergencyContactId, CancellationToken cancellationToken)
    {
        var result = await emergencyContacts.DeleteEmergencyContactAsync(employeeId, emergencyContactId, cancellationToken);
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
        _ => new ActionResult<T>(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };
}
