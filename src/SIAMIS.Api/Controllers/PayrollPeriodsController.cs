using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Manages payroll cycle dates, status, and generation of employee payroll snapshots.</summary>
[ApiController]
[Route("api/payroll-periods")]
[Produces("application/json")]
public sealed class PayrollPeriodsController(IPayrollPeriodService service, IPayrollGenerationService generation, IPayrollPreviewService preview) : ControllerBase
{
    /// <summary>Calculates payroll from current compensation and assignments without saving payroll records.</summary>
    /// <param name="payrollPeriodId">The Open or Processing payroll period to preview.</param>
    /// <param name="request">Optional employee IDs. Omit or provide an empty list to preview all active employees.</param>
    [HttpPost("{payrollPeriodId:guid}/preview")]
    [ProducesResponseType(typeof(PayrollPreviewSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollPreviewSummary>> PreviewPayrolls(
        Guid payrollPeriodId, [FromBody] PayrollPreviewRequest request, CancellationToken ct)
    {
        var result = await preview.PreviewAsync(payrollPeriodId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PayrollPreviewSummary>(result.Failure!);
    }

    /// <summary>Generates payroll snapshots for active employees or the supplied employee IDs. Each employee is processed atomically.</summary>
    /// <param name="payrollPeriodId">The payroll period to generate.</param>
    /// <param name="request">Optional employee selection and force-regeneration settings.</param>
    [HttpPost("{payrollPeriodId:guid}/generate")]
    [ProducesResponseType(typeof(PayrollGenerationSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollGenerationSummary>> GeneratePayrolls(
        Guid payrollPeriodId, [FromBody] PayrollGenerationRequest request, CancellationToken ct)
    {
        var result = await generation.GenerateAsync(payrollPeriodId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PayrollGenerationSummary>(result.Failure!);
    }

    /// <summary>Lists payroll periods with optional status, date-overlap and text filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PayrollPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PayrollPeriodDto>>> GetPayrollPeriods(
        [FromQuery] string? status, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] string? search, CancellationToken ct)
    {
        var result = await service.GetPayrollPeriodsAsync(status, fromDate, toDate, search, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<IReadOnlyList<PayrollPeriodDto>>(result.Failure!);
    }

    /// <summary>Returns one payroll period.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PayrollPeriodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayrollPeriodDto>> GetPayrollPeriod(Guid id, CancellationToken ct)
    {
        var period = await service.GetPayrollPeriodAsync(id, ct);
        return period is null ? NotFoundProblem() : Ok(period);
    }

    /// <summary>Creates a payroll period. Status defaults to Open when omitted.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PayrollPeriodDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollPeriodDto>> CreatePayrollPeriod([FromBody] PayrollPeriodRequest request, CancellationToken ct)
    {
        var result = await service.CreatePayrollPeriodAsync(request, ct);
        if (!result.IsSuccess) return Failure<PayrollPeriodDto>(result.Failure!);
        return CreatedAtAction(nameof(GetPayrollPeriod), new { id = result.Value!.PayrollPeriodId }, result.Value);
    }

    /// <summary>Updates a payroll period's identifying data, dates, status and remarks.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PayrollPeriodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollPeriodDto>> UpdatePayrollPeriod(Guid id, [FromBody] PayrollPeriodRequest request, CancellationToken ct)
    {
        var result = await service.UpdatePayrollPeriodAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : Failure<PayrollPeriodDto>(result.Failure!);
    }

    /// <summary>Changes only the payroll period status.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] PayrollPeriodStatusRequest request, CancellationToken ct)
    {
        var result = await service.SetPayrollPeriodStatusAsync(id, request.Status, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    /// <summary>Deletes a payroll period.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayrollPeriod(Guid id, CancellationToken ct)
    {
        var result = await service.DeletePayrollPeriodAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] })
        { Title = "One or more validation errors occurred.", Status = StatusCodes.Status400BadRequest })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = StatusCodes.Status404NotFound })),
        "conflict" => new(Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: failure.Message)),
        "configuration" => new(Problem(statusCode: StatusCodes.Status409Conflict, title: "Payroll configuration error", detail: failure.Message)),
        _ => new(Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error"))
    };

    private ObjectResult NotFoundProblem() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Payroll period was not found.");
}
