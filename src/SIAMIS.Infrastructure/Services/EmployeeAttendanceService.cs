using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeAttendanceService(SIAMISDbContext db) : IEmployeeAttendanceService
{
    public async Task<ServiceResult<IReadOnlyList<EmployeeAttendanceDto>>> GetAttendanceAsync(Guid employeeId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<IReadOnlyList<EmployeeAttendanceDto>>("Employee was not found.");
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            return Invalid<IReadOnlyList<EmployeeAttendanceDto>>("fromDate cannot be after toDate.");

        var query = AttendanceQuery().Where(x => x.EmployeeId == employeeId);
        if (fromDate.HasValue) query = query.Where(x => x.AttendanceDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.AttendanceDate <= toDate.Value);
        var records = await query.OrderByDescending(x => x.AttendanceDate).ThenByDescending(x => x.AttendanceId).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeAttendanceDto>>.Success(records);
    }

    public async Task<ServiceResult<EmployeeAttendanceDto>> GetAttendanceRecordAsync(Guid employeeId, Guid attendanceId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeAttendanceDto>("Employee was not found.");
        var item = await AttendanceQuery().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.AttendanceId == attendanceId, ct);
        return item is null
            ? NotFound<EmployeeAttendanceDto>("Attendance record was not found for this employee.")
            : ServiceResult<EmployeeAttendanceDto>.Success(item);
    }

    public Task<ServiceResult<EmployeeAttendanceDto>> CreateAttendanceAsync(Guid employeeId, EmployeeAttendanceRequest request, CancellationToken ct)
        => Task.FromResult(ServiceResult<EmployeeAttendanceDto>.Fail("retired", "Legacy attendance writes are retired. Use the Development-only manual attendance-event API."));

    public Task<ServiceResult<EmployeeAttendanceDto>> UpdateAttendanceAsync(Guid employeeId, Guid attendanceId, EmployeeAttendanceRequest request, CancellationToken ct)
        => Task.FromResult(ServiceResult<EmployeeAttendanceDto>.Fail("retired", "Legacy attendance writes are retired. Observations cannot be rewritten."));

    public Task<ServiceResult<bool>> DeleteAttendanceAsync(Guid employeeId, Guid attendanceId, CancellationToken ct)
        => Task.FromResult(ServiceResult<bool>.Fail("retired", "Legacy attendance writes are retired. Legacy storage is retained."));

    private IQueryable<EmployeeAttendanceDto> AttendanceQuery() => db.Attendance.AsNoTracking().Select(x => new EmployeeAttendanceDto
    {
        AttendanceId = x.AttendanceId,
        EmployeeId = x.EmployeeId,
        AttendanceDate = x.AttendanceDate,
        AttendanceStatusId = x.AttendanceStatusId,
        AttendanceStatusCode = x.AttendanceStatus.Code,
        AttendanceStatusName = x.AttendanceStatus.Name,
        CheckIn = x.CheckIn,
        CheckOut = x.CheckOut,
        Remarks = x.Remarks
    });

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct);
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
}
