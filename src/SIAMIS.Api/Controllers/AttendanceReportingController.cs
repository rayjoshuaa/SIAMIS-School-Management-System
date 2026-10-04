using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Employees;

namespace SIAMIS.Api.Controllers;

/// <summary>Factual read-only attendance operations. No employee identity or authorization is implied by an employeeId.</summary>
[ApiController]
[Produces("application/json")]
public sealed class AttendanceReportingController(IAttendanceReportingService service) : ControllerBase
{
    /// <summary>Selected-date effective staff, live facts and validated official facts. Counts cover all matching rows before pagination; overlapping categories are not exclusive. EmployeeNumber/EmployeeId order. Max 500 candidate employees, PageSize 1–100. Typed filters narrow factual state; configuration problems remain structured rows. No inference of current presence.</summary>
    [HttpGet("api/attendance/days/{date}")]
    [ProducesResponseType(typeof(AttendanceOverviewDto),200)]
    [ProducesResponseType(typeof(ValidationProblemDetails),400)]
    [ProducesResponseType(typeof(ProblemDetails),409)]
    public async Task<ActionResult<AttendanceOverviewDto>> Day(DateOnly date,[FromQuery] AttendanceReportFilter query,CancellationToken ct)=>Result(await service.DailyAsync(date,query,ct));

    /// <summary>Today's overview, derived from UTC in Asia/Bangkok, never server-local date. Same bounded filters/pagination as selected date. PotentialAbsence is provisional even before a shift; schedule/arrival windows are factual only.</summary>
    [HttpGet("api/attendance/today")]
    [ProducesResponseType(typeof(AttendanceOverviewDto),200)]
    [ProducesResponseType(typeof(ValidationProblemDetails),400)]
    [ProducesResponseType(typeof(ProblemDetails),409)]
    public async Task<ActionResult<AttendanceOverviewDto>> Today([FromQuery] AttendanceReportFilter query,CancellationToken ct)=>Result(await service.DailyAsync(null,query,ct));

    /// <summary>Employee history: required inclusive From/To, maximum 366 days; dates outside effective employment omitted. Ascending business date. Live and official facts separate; stale/reopened revisions never masquerade as official. No private Leave evidence or payroll data.</summary>
    [HttpGet("api/employees/{employeeId:guid}/attendance-history")]
    [ProducesResponseType(typeof(AttendanceHistoryDto),200)]
    [ProducesResponseType(typeof(ValidationProblemDetails),400)]
    [ProducesResponseType(typeof(ProblemDetails),404)]
    [ProducesResponseType(typeof(ProblemDetails),409)]
    public async Task<ActionResult<AttendanceHistoryDto>> History(Guid employeeId,[FromQuery] AttendanceRangeQuery query,CancellationToken ct)=>Result(await service.HistoryAsync(employeeId,query,ct));

    /// <summary>Official bounded period/month summary: required inclusive From/To, maximum 366 days. Monetary-free totals use only latest currently validated frozen revisions; missing/stale/reopened/configuration dates reported separately. Counts are dates, never arbitrary equivalent days. Residual is precision metadata only. Completeness covers effective scheduled dates, not an assertion of employment outside the range.</summary>
    [HttpGet("api/employees/{employeeId:guid}/attendance-summary")]
    [ProducesResponseType(typeof(AttendanceSummaryDto),200)]
    [ProducesResponseType(typeof(ValidationProblemDetails),400)]
    [ProducesResponseType(typeof(ProblemDetails),404)]
    [ProducesResponseType(typeof(ProblemDetails),409)]
    public async Task<ActionResult<AttendanceSummaryDto>> Summary(Guid employeeId,[FromQuery] AttendanceRangeQuery query,CancellationToken ct)=>Result(await service.SummaryAsync(employeeId,query,ct));

    /// <summary>Combined attention/finalization queue, computed without creating review records. Required From/To at most 31 days, 500 candidates and 2000 employee/date calculations; narrow department/position/range if exceeded. Same typed filters/PageSize 1–100. Date/EmployeeNumber/EmployeeId order. Counts are overlapping employee/date categories except EffectiveEmployees (distinct staff), over matching queue rows before pagination.</summary>
    [HttpGet("api/attendance/review-queue")]
    [ProducesResponseType(typeof(AttendanceAttentionQueueDto),200)]
    [ProducesResponseType(typeof(ValidationProblemDetails),400)]
    [ProducesResponseType(typeof(ProblemDetails),409)]
    public async Task<ActionResult<AttendanceAttentionQueueDto>> Queue([FromQuery] AttendanceQueueQuery query,CancellationToken ct)=>Result(await service.QueueAsync(query,ct));

    private ActionResult<T> Result<T>(ServiceResult<T> r)=>r.IsSuccess ? Ok(r.Value) : r.Failure!.Code switch
    {
        "validation"=>BadRequest(new ValidationProblemDetails(new Dictionary<string,string[]>{["query"]=[r.Failure.Message]}){Status=400}),
        "not_found"=>NotFound(new ProblemDetails{Status=404,Title="Not found",Detail=r.Failure.Message}),
        "conflict"=>Conflict(new ProblemDetails{Status=409,Title="Concurrent attendance context",Detail=r.Failure.Message}),
        _=>Problem(statusCode:500)
    };
}
