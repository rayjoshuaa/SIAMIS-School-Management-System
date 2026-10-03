using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Leave;

namespace SIAMIS.Api.Controllers;

/// <summary>Typed leave foundation administration. Authorization is deferred to Production Hardening.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class LeaveFoundationController(ILeaveFoundationService service) : ControllerBase
{
    /// <summary>Calendars for the D8B leave foundation.</summary>
    [HttpGet("work-calendars")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkCalendarDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkCalendarDto>>> Calendars(CancellationToken ct)
    {
        return Ok(await service.CalendarsAsync(ct));
    }

    /// <summary>CreateCalendar for the D8B leave foundation.</summary>
    [HttpPost("work-calendars")]
    [ProducesResponseType(typeof(WorkCalendarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<WorkCalendarDto>> CreateCalendar([FromBody] WorkCalendarRequest request, CancellationToken ct)
    {
        var result = await service.SaveCalendarAsync(null, request, ct);
        if (!result.IsSuccess) return Failure<WorkCalendarDto>(result.Failure!);
        return Created("/api/work-calendars", result.Value);
    }

    /// <summary>UpdateCalendar for the D8B leave foundation.</summary>
    [HttpPut("work-calendars/{calendarId:guid}")]
    [ProducesResponseType(typeof(WorkCalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<WorkCalendarDto>> UpdateCalendar(Guid calendarId, [FromBody] WorkCalendarRequest request, CancellationToken ct)
    {
        var result = await service.SaveCalendarAsync(calendarId, request, ct);
        if (!result.IsSuccess) return Failure<WorkCalendarDto>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>WeeklyIntervals for the D8B leave foundation.</summary>
    [HttpGet("work-calendars/{calendarId:guid}/weekly-intervals")]
    [ProducesResponseType(typeof(IReadOnlyList<WeeklyIntervalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<IReadOnlyList<WeeklyIntervalDto>>> WeeklyIntervals(Guid calendarId, CancellationToken ct)
    {
        var result = await service.WeeklyAsync(calendarId, ct);
        if (!result.IsSuccess) return Failure<IReadOnlyList<WeeklyIntervalDto>>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>CreateWeeklyInterval for the D8B leave foundation.</summary>
    [HttpPost("work-calendars/{calendarId:guid}/weekly-intervals")]
    [ProducesResponseType(typeof(WeeklyIntervalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<WeeklyIntervalDto>> CreateWeeklyInterval(Guid calendarId, [FromBody] WeeklyIntervalRequest request, CancellationToken ct)
    {
        var result = await service.AddWeeklyAsync(calendarId, request, ct);
        if (!result.IsSuccess) return Failure<WeeklyIntervalDto>(result.Failure!);
        return Created($"/api/work-calendars/{calendarId}/weekly-intervals", result.Value);
    }

    /// <summary>Overrides for the D8B leave foundation.</summary>
    [HttpGet("work-calendars/{calendarId:guid}/overrides")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarOverrideDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<IReadOnlyList<CalendarOverrideDto>>> Overrides(Guid calendarId, CancellationToken ct)
    {
        var result = await service.OverridesAsync(calendarId, ct);
        if (!result.IsSuccess) return Failure<IReadOnlyList<CalendarOverrideDto>>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>Creates a dated override. Exceptional intervals replace weekly hours; non-working overrides contain no intervals.</summary>
    [HttpPost("work-calendars/{calendarId:guid}/overrides")]
    [ProducesResponseType(typeof(CalendarOverrideDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<CalendarOverrideDto>> CreateOverride(Guid calendarId, [FromBody] CalendarOverrideRequest request, CancellationToken ct)
    {
        var result = await service.AddOverrideAsync(calendarId, request, ct);
        if (!result.IsSuccess) return Failure<CalendarOverrideDto>(result.Failure!);
        return Created($"/api/work-calendars/{calendarId}/overrides", result.Value);
    }

    /// <summary>Assignments for the D8B leave foundation.</summary>
    [HttpGet("employees/{employeeId:guid}/work-calendar-assignments")]
    [ProducesResponseType(typeof(IReadOnlyList<CalendarAssignmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<IReadOnlyList<CalendarAssignmentDto>>> Assignments(Guid employeeId, CancellationToken ct)
    {
        var result = await service.AssignmentsAsync(employeeId, ct);
        if (!result.IsSuccess) return Failure<IReadOnlyList<CalendarAssignmentDto>>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>CreateAssignment for the D8B leave foundation.</summary>
    [HttpPost("employees/{employeeId:guid}/work-calendar-assignments")]
    [ProducesResponseType(typeof(CalendarAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<CalendarAssignmentDto>> CreateAssignment(Guid employeeId, [FromBody] CalendarAssignmentRequest request, CancellationToken ct)
    {
        var result = await service.AssignAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<CalendarAssignmentDto>(result.Failure!);
        return Created($"/api/employees/{employeeId}/work-calendar-assignments", result.Value);
    }

    /// <summary>Resolves an explicit assignment and replacement schedule for date; no default fallback. Missing or ambiguous coverage returns 409.</summary>
    [HttpGet("employees/{employeeId:guid}/work-calendar")]
    [ProducesResponseType(typeof(CalendarResolutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<CalendarResolutionDto>> ResolveCalendar(Guid employeeId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var result = await service.ResolveCalendarAsync(employeeId, date, ct);
        if (!result.IsSuccess) return Failure<CalendarResolutionDto>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>Policies for the D8B leave foundation.</summary>
    [HttpGet("leave-policies")]
    [ProducesResponseType(typeof(IReadOnlyList<LeavePolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LeavePolicyDto>>> Policies([FromQuery] Guid? leaveTypeId, CancellationToken ct)
    {
        return Ok(await service.PoliciesAsync(leaveTypeId, ct));
    }

    /// <summary>CreatePolicy for the D8B leave foundation.</summary>
    [HttpPost("leave-policies")]
    [ProducesResponseType(typeof(LeavePolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeavePolicyDto>> CreatePolicy([FromBody] LeavePolicyRequest request, CancellationToken ct)
    {
        var result = await service.SavePolicyAsync(null, request, ct);
        if (!result.IsSuccess) return Failure<LeavePolicyDto>(result.Failure!);
        return Created("/api/leave-policies", result.Value);
    }

    /// <summary>UpdateDraftPolicy for the D8B leave foundation.</summary>
    [HttpPut("leave-policies/{policyId:guid}")]
    [ProducesResponseType(typeof(LeavePolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeavePolicyDto>> UpdateDraftPolicy(Guid policyId, [FromBody] LeavePolicyRequest request, CancellationToken ct)
    {
        var result = await service.SavePolicyAsync(policyId, request, ct);
        if (!result.IsSuccess) return Failure<LeavePolicyDto>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>Publishes an immutable policy revision; overlapping inclusive coverage returns 409.</summary>
    [HttpPost("leave-policies/{policyId:guid}/publish")]
    [ProducesResponseType(typeof(LeavePolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeavePolicyDto>> PublishPolicy(Guid policyId, CancellationToken ct)
    {
        var result = await service.PublishPolicyAsync(policyId, ct);
        if (!result.IsSuccess) return Failure<LeavePolicyDto>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>ResolvePolicy for the D8B leave foundation.</summary>
    [HttpGet("leave-policies/resolve")]
    [ProducesResponseType(typeof(LeavePolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<LeavePolicyDto>> ResolvePolicy([FromQuery] Guid leaveTypeId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var result = await service.ResolvePolicyAsync(leaveTypeId, date, ct);
        if (!result.IsSuccess) return Failure<LeavePolicyDto>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>Entitlements for the D8B leave foundation.</summary>
    [HttpGet("employees/{employeeId:guid}/leave-entitlements")]
    [ProducesResponseType(typeof(IReadOnlyList<EntitlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<IReadOnlyList<EntitlementDto>>> Entitlements(Guid employeeId, CancellationToken ct)
    {
        var result = await service.EntitlementsAsync(employeeId, ct);
        if (!result.IsSuccess) return Failure<IReadOnlyList<EntitlementDto>>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>Creates a calendar-year entitlement in minutes. Missing records are distinct from configured zero.</summary>
    [HttpPost("employees/{employeeId:guid}/leave-entitlements")]
    [ProducesResponseType(typeof(EntitlementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<EntitlementDto>> CreateEntitlement(Guid employeeId, [FromBody] EntitlementRequest request, CancellationToken ct)
    {
        var result = await service.AddEntitlementAsync(employeeId, request, ct);
        if (!result.IsSuccess) return Failure<EntitlementDto>(result.Failure!);
        return Created($"/api/employees/{employeeId}/leave-entitlements", result.Value);
    }

    /// <summary>Adjustments for the D8B leave foundation.</summary>
    [HttpGet("employees/{employeeId:guid}/leave-entitlements/{entitlementId:guid}/adjustments")]
    [ProducesResponseType(typeof(IReadOnlyList<EntitlementAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<IReadOnlyList<EntitlementAdjustmentDto>>> Adjustments(Guid employeeId, Guid entitlementId, CancellationToken ct)
    {
        var result = await service.AdjustmentsAsync(employeeId, entitlementId, ct);
        if (!result.IsSuccess) return Failure<IReadOnlyList<EntitlementAdjustmentDto>>(result.Failure!);
        return Ok(result.Value);
    }

    /// <summary>Appends a signed whole-minute adjustment with a required reason; existing entries cannot be edited.</summary>
    [HttpPost("employees/{employeeId:guid}/leave-entitlements/{entitlementId:guid}/adjustments")]
    [ProducesResponseType(typeof(EntitlementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public async Task<ActionResult<EntitlementDto>> CreateAdjustment(Guid employeeId, Guid entitlementId, [FromBody] EntitlementAdjustmentRequest request, CancellationToken ct)
    {
        var result = await service.AdjustAsync(employeeId, entitlementId, request, ct);
        if (!result.IsSuccess) return Failure<EntitlementDto>(result.Failure!);
        return Created($"/api/employees/{employeeId}/leave-entitlements/{entitlementId}/adjustments", result.Value);
    }

    private ActionResult<T> Failure<T>(ApiFailure failure) => failure.Code switch
    {
        "validation" => new(BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["request"] = [failure.Message] }) { Status = 400 })),
        "not_found" => new(NotFound(new ProblemDetails { Title = "Not found", Detail = failure.Message, Status = 404 })),
        "conflict" => new(Conflict(new ProblemDetails { Title = "Conflict", Detail = failure.Message, Status = 409 })),
        _ => new(Problem(statusCode: 500, title: "Unexpected error"))
    };
}
