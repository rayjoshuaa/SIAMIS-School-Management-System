using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Reviewed regular monthly PIT schedule revisions. Verified facts are immutable.</summary>
[Route("api/employees/{employeeId:guid}/pit-payment-schedules")]
public sealed class EmployeePitPaymentSchedulesController(IPitPaymentScheduleService service) : StatutoryConfigurationController
{
    /// <summary>Creates a Draft, replacing the selected revision when one exists. Never drives calculation until verified.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PitPaymentScheduleDto), 201)]
    [ProducesResponseType(400)] [ProducesResponseType(404)] [ProducesResponseType(409)]
    public async Task<ActionResult<PitPaymentScheduleDto>> Create(Guid employeeId, PitPaymentScheduleRequest request, CancellationToken ct)
    {
        var result = await service.CreateAsync(employeeId, request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { employeeId, id = result.Value!.EmployeePitPaymentScheduleId }, result.Value) : Failure<PitPaymentScheduleDto>(result.Failure!);
    }
    /// <summary>Inspects a schedule revision, including its reviewed evidence and ordered entries.</summary>
    [HttpGet("{id:guid}")] [ProducesResponseType(typeof(PitPaymentScheduleDto), 200)] [ProducesResponseType(404)]
    public async Task<ActionResult<PitPaymentScheduleDto>> Get(Guid employeeId, Guid id, CancellationToken ct) => Respond(await service.GetAsync(employeeId, id, ct));
    /// <summary>Corrects only a Draft's evidence and entries. Employee/year identity and Verified revisions are protected.</summary>
    [HttpPut("{id:guid}")] [ProducesResponseType(typeof(PitPaymentScheduleDto), 200)]
    [ProducesResponseType(400)] [ProducesResponseType(404)] [ProducesResponseType(409)]
    public async Task<ActionResult<PitPaymentScheduleDto>> Update(Guid employeeId, Guid id, PitPaymentScheduleRequest request, CancellationToken ct)
        => Respond(await service.UpdateDraftAsync(employeeId, id, request, ct));
    /// <summary>Validates and atomically verifies/selects a Draft. Verified revisions cannot be edited or reselected through this endpoint.</summary>
    [HttpPost("{id:guid}/verify")] [ProducesResponseType(typeof(PitPaymentScheduleDto), 200)]
    [ProducesResponseType(400)] [ProducesResponseType(404)] [ProducesResponseType(409)]
    public async Task<ActionResult<PitPaymentScheduleDto>> Verify(Guid employeeId, Guid id, CancellationToken ct) => Respond(await service.VerifyAsync(employeeId, id, ct));
    /// <summary>Reads the selected Verified schedule for the supplied Gregorian tax year; null when absent.</summary>
    [HttpGet("current/{taxYear:int}")] [ProducesResponseType(typeof(PitPaymentScheduleDto), 200)] [ProducesResponseType(404)]
    public async Task<ActionResult<PitPaymentScheduleDto?>> Current(Guid employeeId, [System.ComponentModel.DataAnnotations.Range(1, 9999)] int taxYear, CancellationToken ct)
    {
        var result = await service.CurrentAsync(employeeId, taxYear, ct);
        return result.IsSuccess ? new JsonResult(result.Value) { StatusCode = 200 } : Failure<PitPaymentScheduleDto?>(result.Failure!);
    }
}
