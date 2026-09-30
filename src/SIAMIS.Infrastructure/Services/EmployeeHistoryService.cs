using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeHistoryService(SIAMISDbContext db) : IEmployeeHistoryService
{
    private static readonly IReadOnlyDictionary<string, string> ApprovedEventTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Joined"] = "Joined",
        ["Department Transfer"] = "Department Transfer",
        ["Designation Change"] = "Designation Change",
        ["Promotion"] = "Promotion",
        ["Salary Change"] = "Salary Change",
        ["Contract Renewal"] = "Contract Renewal",
        ["Contract Change"] = "Contract Change",
        ["Leave of Absence"] = "Leave of Absence",
        ["Status Change"] = "Status Change",
        ["Return From Leave"] = "Return From Leave",
        ["Resignation"] = "Resignation",
        ["Termination"] = "Termination",
        ["Retirement"] = "Retirement",
        ["Other"] = "Other"
    };

    public async Task<ServiceResult<IReadOnlyList<EmployeeHistoryDto>>> GetHistoryAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<IReadOnlyList<EmployeeHistoryDto>>("Employee was not found.");
        var events = await HistoryQuery().Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EventDate)
            .ThenByDescending(x => x.EmployeeHistoryId)
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeHistoryDto>>.Success(events);
    }

    public async Task<ServiceResult<EmployeeHistoryDto>> GetHistoryEventAsync(Guid employeeId, Guid historyId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeHistoryDto>("Employee was not found.");
        var item = await HistoryQuery().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeHistoryId == historyId, ct);
        return item is null
            ? NotFound<EmployeeHistoryDto>("History event was not found for this employee.")
            : ServiceResult<EmployeeHistoryDto>.Success(item);
    }

    public async Task<ServiceResult<EmployeeHistoryDto>> CreateHistoryEventAsync(Guid employeeId, CreateEmployeeHistoryRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeHistoryDto>("Employee was not found.");
        if (string.IsNullOrWhiteSpace(request.EventType) || !ApprovedEventTypes.TryGetValue(request.EventType.Trim(), out var eventType))
            return Invalid<EmployeeHistoryDto>("EventType must be one of the approved employee history event types.");
        if (!request.EventDate.HasValue) return Invalid<EmployeeHistoryDto>("EventDate is required.");
        if (request.PreviousValue?.Length > 4000 || request.NewValue?.Length > 4000)
            return Invalid<EmployeeHistoryDto>("PreviousValue and NewValue cannot exceed 4000 characters.");
        if (request.Description?.Length > 2000) return Invalid<EmployeeHistoryDto>("Description cannot exceed 2000 characters.");
        if (request.ChangedBy?.Length > 100) return Invalid<EmployeeHistoryDto>("ChangedBy cannot exceed 100 characters.");

        var history = new EmployeeHistory
        {
            EmployeeId = employeeId,
            EventType = eventType,
            EventDate = request.EventDate.Value,
            PreviousValue = Clean(request.PreviousValue),
            NewValue = Clean(request.NewValue),
            Description = Clean(request.Description),
            ChangedBy = Clean(request.ChangedBy)
        };
        db.EmployeeHistory.Add(history);
        await db.SaveChangesAsync(ct);
        return ServiceResult<EmployeeHistoryDto>.Success(ToDto(history));
    }

    public async Task<ServiceResult<bool>> DeleteHistoryEventAsync(Guid employeeId, Guid historyId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var item = await db.EmployeeHistory.SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId && x.EmployeeHistoryId == historyId, ct);
        if (item is null) return NotFound<bool>("History event was not found for this employee.");
        db.EmployeeHistory.Remove(item);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private IQueryable<EmployeeHistoryDto> HistoryQuery() => db.EmployeeHistory.AsNoTracking().Select(x => new EmployeeHistoryDto
    {
        EmployeeHistoryId = x.EmployeeHistoryId,
        EmployeeId = x.EmployeeId,
        EventType = x.EventType,
        EventDate = x.EventDate,
        PreviousValue = x.PreviousValue,
        NewValue = x.NewValue,
        Description = x.Description,
        ChangedBy = x.ChangedBy
    });

    private static EmployeeHistoryDto ToDto(EmployeeHistory item) => new()
    {
        EmployeeHistoryId = item.EmployeeHistoryId,
        EmployeeId = item.EmployeeId,
        EventType = item.EventType,
        EventDate = item.EventDate,
        PreviousValue = item.PreviousValue,
        NewValue = item.NewValue,
        Description = item.Description,
        ChangedBy = item.ChangedBy
    };

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
}
