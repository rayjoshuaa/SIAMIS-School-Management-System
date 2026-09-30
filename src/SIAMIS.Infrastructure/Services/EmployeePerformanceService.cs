using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeePerformanceService(SIAMISDbContext db) : IEmployeePerformanceService
{
    public async Task<ServiceResult<IReadOnlyList<EmployeePerformanceDto>>> GetPerformanceRecordsAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<IReadOnlyList<EmployeePerformanceDto>>("Employee was not found.");
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            return Invalid<IReadOnlyList<EmployeePerformanceDto>>("fromDate cannot be after toDate.");

        var query = PerformanceQuery().Where(x => x.EmployeeId == employeeId);
        if (fromDate.HasValue) query = query.Where(x => x.ReviewDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.ReviewDate <= toDate.Value);
        var records = await query.OrderByDescending(x => x.ReviewDate).ThenByDescending(x => x.PerformanceRecordId).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeePerformanceDto>>.Success(records);
    }

    public async Task<ServiceResult<EmployeePerformanceDto>> GetPerformanceRecordAsync(Guid employeeId, Guid performanceRecordId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeePerformanceDto>("Employee was not found.");
        var record = await PerformanceQuery().SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId && x.PerformanceRecordId == performanceRecordId, ct);
        return record is null
            ? NotFound<EmployeePerformanceDto>("Performance record was not found for this employee.")
            : ServiceResult<EmployeePerformanceDto>.Success(record);
    }

    public async Task<ServiceResult<EmployeePerformanceDto>> CreatePerformanceRecordAsync(Guid employeeId, EmployeePerformanceRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeePerformanceDto>("Employee was not found.");
        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeePerformanceDto>(validation);
        if (request.ReviewerEmployeeId.HasValue && !await EmployeeExists(request.ReviewerEmployeeId.Value, ct))
            return NotFound<EmployeePerformanceDto>("ReviewerEmployeeId does not reference an existing employee.");

        var record = new EmployeePerformance
        {
            EmployeeId = employeeId,
            ReviewDate = request.ReviewDate!.Value,
            ReviewPeriodStart = request.ReviewPeriodStart,
            ReviewPeriodEnd = request.ReviewPeriodEnd,
            PerformanceRatingId = request.PerformanceRatingId!.Value,
            ReviewerEmployeeId = request.ReviewerEmployeeId,
            Strengths = Clean(request.Strengths),
            AreasForImprovement = Clean(request.AreasForImprovement),
            Goals = Clean(request.Goals),
            Remarks = Clean(request.Remarks)
        };
        db.EmployeePerformanceRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return await GetPerformanceRecordAsync(employeeId, record.PerformanceRecordId, ct);
    }

    public async Task<ServiceResult<EmployeePerformanceDto>> UpdatePerformanceRecordAsync(Guid employeeId, Guid performanceRecordId, EmployeePerformanceRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeePerformanceDto>("Employee was not found.");
        var record = await db.EmployeePerformanceRecords.SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId && x.PerformanceRecordId == performanceRecordId, ct);
        if (record is null) return NotFound<EmployeePerformanceDto>("Performance record was not found for this employee.");
        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeePerformanceDto>(validation);
        if (request.ReviewerEmployeeId.HasValue && !await EmployeeExists(request.ReviewerEmployeeId.Value, ct))
            return NotFound<EmployeePerformanceDto>("ReviewerEmployeeId does not reference an existing employee.");

        record.ReviewDate = request.ReviewDate!.Value;
        record.ReviewPeriodStart = request.ReviewPeriodStart;
        record.ReviewPeriodEnd = request.ReviewPeriodEnd;
        record.PerformanceRatingId = request.PerformanceRatingId!.Value;
        record.ReviewerEmployeeId = request.ReviewerEmployeeId;
        record.Strengths = Clean(request.Strengths);
        record.AreasForImprovement = Clean(request.AreasForImprovement);
        record.Goals = Clean(request.Goals);
        record.Remarks = Clean(request.Remarks);
        await db.SaveChangesAsync(ct);
        return await GetPerformanceRecordAsync(employeeId, performanceRecordId, ct);
    }

    public async Task<ServiceResult<bool>> DeletePerformanceRecordAsync(Guid employeeId, Guid performanceRecordId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var record = await db.EmployeePerformanceRecords.SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId && x.PerformanceRecordId == performanceRecordId, ct);
        if (record is null) return NotFound<bool>("Performance record was not found for this employee.");
        db.EmployeePerformanceRecords.Remove(record);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<string?> ValidateRequest(EmployeePerformanceRequest request, CancellationToken ct)
    {
        if (!request.ReviewDate.HasValue) return "ReviewDate is required.";
        if (!request.PerformanceRatingId.HasValue || !await db.PerformanceRatings.AsNoTracking()
                .AnyAsync(x => x.Id == request.PerformanceRatingId && x.IsActive, ct))
            return "PerformanceRatingId must reference an active performance rating.";
        if (request.ReviewPeriodStart.HasValue && request.ReviewPeriodEnd.HasValue)
        {
            if (request.ReviewPeriodEnd.Value < request.ReviewPeriodStart.Value)
                return "ReviewPeriodEnd cannot be before ReviewPeriodStart.";
            if (request.ReviewDate.Value < request.ReviewPeriodStart.Value || request.ReviewDate.Value > request.ReviewPeriodEnd.Value)
                return "ReviewDate must fall within the supplied review period.";
        }
        if (request.Strengths?.Length > 4000 || request.AreasForImprovement?.Length > 4000 || request.Goals?.Length > 4000)
            return "Strengths, AreasForImprovement, and Goals cannot exceed 4000 characters.";
        if (request.Remarks?.Length > 2000) return "Remarks cannot exceed 2000 characters.";
        return null;
    }

    private IQueryable<EmployeePerformanceDto> PerformanceQuery() => db.EmployeePerformanceRecords.AsNoTracking().Select(x => new EmployeePerformanceDto
    {
        PerformanceRecordId = x.PerformanceRecordId,
        EmployeeId = x.EmployeeId,
        ReviewDate = x.ReviewDate,
        ReviewPeriodStart = x.ReviewPeriodStart,
        ReviewPeriodEnd = x.ReviewPeriodEnd,
        PerformanceRatingId = x.PerformanceRatingId,
        PerformanceRatingCode = x.PerformanceRating.Code,
        PerformanceRatingName = x.PerformanceRating.Name,
        ReviewerEmployeeId = x.ReviewerEmployeeId,
        ReviewerEmployeeName = x.ReviewerEmployee == null ? null :
            (x.ReviewerEmployee.PreferredName ?? x.ReviewerEmployee.FirstName) + " " + x.ReviewerEmployee.LastName,
        Strengths = x.Strengths,
        AreasForImprovement = x.AreasForImprovement,
        Goals = x.Goals,
        Remarks = x.Remarks
    });

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
}
