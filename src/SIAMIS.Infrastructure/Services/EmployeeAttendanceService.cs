using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
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

    public async Task<ServiceResult<EmployeeAttendanceDto>> CreateAttendanceAsync(Guid employeeId, EmployeeAttendanceRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeAttendanceDto>("Employee was not found.");
        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeeAttendanceDto>(validation);
        if (await db.Attendance.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId && x.AttendanceDate == request.AttendanceDate, ct))
            return Conflict<EmployeeAttendanceDto>("An attendance record already exists for this employee and date.");

        var item = new EmployeeAttendance
        {
            EmployeeId = employeeId,
            AttendanceDate = request.AttendanceDate!.Value,
            AttendanceStatusId = request.AttendanceStatusId!.Value,
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            Remarks = Clean(request.Remarks)
        };
        db.Attendance.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict<EmployeeAttendanceDto>("An attendance record already exists for this employee and date.");
        }
        return await GetAttendanceRecordAsync(employeeId, item.AttendanceId, ct);
    }

    public async Task<ServiceResult<EmployeeAttendanceDto>> UpdateAttendanceAsync(Guid employeeId, Guid attendanceId, EmployeeAttendanceRequest request, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeAttendanceDto>("Employee was not found.");
        var item = await db.Attendance.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.AttendanceId == attendanceId, ct);
        if (item is null) return NotFound<EmployeeAttendanceDto>("Attendance record was not found for this employee.");
        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeeAttendanceDto>(validation);
        if (await db.Attendance.AsNoTracking().AnyAsync(
                x => x.EmployeeId == employeeId && x.AttendanceDate == request.AttendanceDate && x.AttendanceId != attendanceId, ct))
            return Conflict<EmployeeAttendanceDto>("An attendance record already exists for this employee and date.");

        item.AttendanceDate = request.AttendanceDate!.Value;
        item.AttendanceStatusId = request.AttendanceStatusId!.Value;
        item.CheckIn = request.CheckIn;
        item.CheckOut = request.CheckOut;
        item.Remarks = Clean(request.Remarks);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict<EmployeeAttendanceDto>("An attendance record already exists for this employee and date.");
        }
        return await GetAttendanceRecordAsync(employeeId, attendanceId, ct);
    }

    public async Task<ServiceResult<bool>> DeleteAttendanceAsync(Guid employeeId, Guid attendanceId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var item = await db.Attendance.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.AttendanceId == attendanceId, ct);
        if (item is null) return NotFound<bool>("Attendance record was not found for this employee.");
        db.Attendance.Remove(item);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<string?> ValidateRequest(EmployeeAttendanceRequest request, CancellationToken ct)
    {
        if (!request.AttendanceDate.HasValue) return "AttendanceDate is required.";
        if (!request.AttendanceStatusId.HasValue || !await db.AttendanceStatuses.AsNoTracking()
                .AnyAsync(x => x.Id == request.AttendanceStatusId && x.IsActive, ct))
            return "AttendanceStatusId must reference an active attendance status.";
        if (request.CheckIn.HasValue && request.CheckOut.HasValue && request.CheckOut.Value < request.CheckIn.Value)
            return "CheckOut cannot be earlier than CheckIn.";
        if (request.Remarks?.Length > 2000) return "Remarks cannot exceed 2000 characters.";
        return null;
    }

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
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
}
