using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class PayrollPeriodService(SIAMISDbContext db) : IPayrollPeriodService
{
    private static readonly string[] AllowedStatuses = ["Open", "Processing", "Closed", "Cancelled"];

    public async Task<ServiceResult<IReadOnlyList<PayrollPeriodDto>>> GetPayrollPeriodsAsync(
        string? status, DateOnly? fromDate, DateOnly? toDate, string? search, CancellationToken cancellationToken)
    {
        var canonicalStatus = status is null ? null : NormalizeStatus(status);
        if (status is not null && canonicalStatus is null)
            return Validation<IReadOnlyList<PayrollPeriodDto>>("Status must be Open, Processing, Closed, or Cancelled.");
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            return Validation<IReadOnlyList<PayrollPeriodDto>>("fromDate cannot be after toDate.");

        IQueryable<PayrollPeriod> query = db.PayrollPeriods.AsNoTracking();
        if (canonicalStatus is not null)
            query = query.Where(item => item.Status == canonicalStatus);
        if (fromDate.HasValue)
            query = query.Where(item => item.EndDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(item => item.StartDate <= toDate.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || item.Code.Contains(term) || (item.Remarks != null && item.Remarks.Contains(term)));
        }

        IReadOnlyList<PayrollPeriodDto> periods = await query
            .OrderByDescending(item => item.StartDate).ThenByDescending(item => item.PayrollPeriodId)
            .Select(item => new PayrollPeriodDto(item.PayrollPeriodId, item.Code, item.Name, item.StartDate, item.EndDate,
                item.PayDate, item.Status, item.Remarks, item.CreatedAt, item.UpdatedAt,
                item.ProcessingStartedAt, item.ClosedAt, item.CancelledAt, item.CancellationReason))
            .ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<PayrollPeriodDto>>.Success(periods);
    }

    public async Task<PayrollPeriodDto?> GetPayrollPeriodAsync(Guid id, CancellationToken cancellationToken)
        => await db.PayrollPeriods.AsNoTracking().Where(item => item.PayrollPeriodId == id)
            .Select(item => new PayrollPeriodDto(item.PayrollPeriodId, item.Code, item.Name, item.StartDate, item.EndDate,
                item.PayDate, item.Status, item.Remarks, item.CreatedAt, item.UpdatedAt,
                item.ProcessingStartedAt, item.ClosedAt, item.CancelledAt, item.CancellationReason)).SingleOrDefaultAsync(cancellationToken);

    public async Task<ServiceResult<PayrollPeriodDto>> CreatePayrollPeriodAsync(PayrollPeriodRequest request, CancellationToken cancellationToken)
    {
        var values = Normalize(request);
        if (values.Failure is not null) return ServiceResult<PayrollPeriodDto>.Fail(values.Failure.Code, values.Failure.Message);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await db.PayrollPeriods.AnyAsync(item => item.Code == values.Code, cancellationToken))
            return Conflict<PayrollPeriodDto>("A payroll period with this code already exists.");
        if (await HasOverlapAsync(values.StartDate!.Value, values.EndDate!.Value, null, cancellationToken))
            return Conflict<PayrollPeriodDto>("Payroll periods cannot overlap.");

        var period = new PayrollPeriod
        {
            Code = values.Code!, Name = values.Name!, StartDate = values.StartDate.Value,
            EndDate = values.EndDate.Value, PayDate = values.PayDate!.Value, Status = "Open", Remarks = values.Remarks
        };
        db.PayrollPeriods.Add(period);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueCodeViolation(exception))
        {
            return Conflict<PayrollPeriodDto>("A payroll period with this code already exists.");
        }
        return ServiceResult<PayrollPeriodDto>.Success(ToDto(period));
    }

    public async Task<ServiceResult<PayrollPeriodDto>> UpdatePayrollPeriodAsync(Guid id, PayrollPeriodRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await PayrollPeriodLock.GetAsync(db, id, cancellationToken);
        if (period is null) return NotFound<PayrollPeriodDto>();
        if (!PayrollPeriodLock.AllowsMutation(period.Status))
            return Conflict<PayrollPeriodDto>($"{period.Status} payroll period is terminal and cannot be updated.");
        var values = Normalize(request);
        if (values.Failure is not null) return ServiceResult<PayrollPeriodDto>.Fail(values.Failure.Code, values.Failure.Message);
        var identityChanged = period.Code != values.Code || period.StartDate != values.StartDate
            || period.EndDate != values.EndDate || period.PayDate != values.PayDate;
        if (identityChanged && (period.Status != "Open" || await db.EmployeePayrolls.AnyAsync(item => item.PayrollPeriodId == id, cancellationToken)))
            return Conflict<PayrollPeriodDto>("Code and period dates can only change on an Open period with no employee payrolls.");
        if (await db.PayrollPeriods.AnyAsync(item => item.PayrollPeriodId != id && item.Code == values.Code, cancellationToken))
            return Conflict<PayrollPeriodDto>("A payroll period with this code already exists.");
        if ((period.StartDate != values.StartDate || period.EndDate != values.EndDate)
            && await HasOverlapAsync(values.StartDate!.Value, values.EndDate!.Value, id, cancellationToken))
            return Conflict<PayrollPeriodDto>("Payroll periods cannot overlap.");

        period.Code = values.Code!;
        period.Name = values.Name!;
        period.StartDate = values.StartDate.Value;
        period.EndDate = values.EndDate.Value;
        period.PayDate = values.PayDate!.Value;
        period.Remarks = values.Remarks;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueCodeViolation(exception))
        {
            return Conflict<PayrollPeriodDto>("A payroll period with this code already exists.");
        }
        return ServiceResult<PayrollPeriodDto>.Success(ToDto(period));
    }

    // Authorization and actor attribution will be added when SIAMIS authentication exists.
    public async Task<ServiceResult<bool>> StartProcessingAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await PayrollPeriodLock.GetAsync(db, id, cancellationToken);
        if (period is null) return NotFound<bool>();
        if (period.Status != "Open") return Conflict<bool>($"Only Open periods can start processing. Current status: {period.Status}.");
        period.Status = "Processing";
        period.ProcessingStartedAt = DateTime.UtcNow;
        period.ClosedAt = null;
        period.CancelledAt = null;
        period.CancellationReason = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> CloseAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await PayrollPeriodLock.GetAsync(db, id, cancellationToken);
        if (period is null) return NotFound<bool>();
        if (period.Status != "Processing") return Conflict<bool>($"Only Processing periods can close. Current status: {period.Status}.");
        if (!await db.EmployeePayrolls.AnyAsync(item => item.PayrollPeriodId == id, cancellationToken))
            return Conflict<bool>("Cannot close an empty period; at least one existing employee payroll is required.");
        // This validates existing snapshots only; it does not establish an expected employee population.
        if (await db.EmployeePayrolls.AnyAsync(item => item.PayrollPeriodId == id && item.Status != "Paid" && item.Status != "Cancelled", cancellationToken))
            return Conflict<bool>("Cannot close period; every existing employee payroll must be Paid or Cancelled. Draft, Calculated and Approved payrolls remain unfinished.");
        period.Status = "Closed";
        period.ClosedAt = DateTime.UtcNow;
        period.CancelledAt = null;
        period.CancellationReason = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> CancelAsync(Guid id, PayrollPeriodCancelRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await PayrollPeriodLock.GetAsync(db, id, cancellationToken);
        if (period is null) return NotFound<bool>();
        if (!PayrollPeriodLock.AllowsMutation(period.Status))
            return Conflict<bool>($"Only Open or Processing periods can be cancelled. Current status: {period.Status}.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return Validation<bool>("Cancellation reason is required and cannot exceed 1000 characters.");
        if (await db.EmployeePayrolls.AnyAsync(item => item.PayrollPeriodId == id && (item.Status == "Approved" || item.Status == "Paid"), cancellationToken))
            return Conflict<bool>("Cannot cancel a period containing Approved or Paid employee payrolls. A future reversal workflow is required.");
        period.Status = "Cancelled";
        period.CancelledAt = DateTime.UtcNow;
        period.CancellationReason = request.Reason.Trim();
        period.ClosedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeletePayrollPeriodAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var period = await PayrollPeriodLock.GetAsync(db, id, cancellationToken);
        if (period is null) return NotFound<bool>();
        if (period.Status != "Open") return Conflict<bool>("Only an empty Open payroll period can be deleted.");
        if (await db.EmployeePayrolls.AnyAsync(item => item.PayrollPeriodId == id, cancellationToken))
            return Conflict<bool>("Payroll period cannot be deleted while employee payrolls exist.");
        db.PayrollPeriods.Remove(period);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private Task<bool> HasOverlapAsync(DateOnly startDate, DateOnly endDate, Guid? excludedId, CancellationToken cancellationToken)
        => db.PayrollPeriods.AnyAsync(item => (excludedId == null || item.PayrollPeriodId != excludedId.Value)
            && item.StartDate <= endDate && item.EndDate >= startDate, cancellationToken);

    private static (string? Code, string? Name, DateOnly? StartDate, DateOnly? EndDate, DateOnly? PayDate, string? Remarks, ApiFailure? Failure)
        Normalize(PayrollPeriodRequest request)
    {
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code)) return (null, null, null, null, null, null, new("validation", "Code is required."));
        if (string.IsNullOrWhiteSpace(name)) return (null, null, null, null, null, null, new("validation", "Name is required."));
        if (!request.StartDate.HasValue) return (null, null, null, null, null, null, new("validation", "StartDate is required."));
        if (!request.EndDate.HasValue) return (null, null, null, null, null, null, new("validation", "EndDate is required."));
        if (!request.PayDate.HasValue) return (null, null, null, null, null, null, new("validation", "PayDate is required."));
        if (request.EndDate.Value < request.StartDate.Value) return (null, null, null, null, null, null, new("validation", "EndDate cannot be before StartDate."));
        if (request.PayDate.Value < request.StartDate.Value) return (null, null, null, null, null, null, new("validation", "PayDate cannot be before StartDate."));
        return (code, name, request.StartDate, request.EndDate, request.PayDate,
            string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(), null);
    }

    private static string? NormalizeStatus(string? status)
        => AllowedStatuses.FirstOrDefault(value => value.Equals(status?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static PayrollPeriodDto ToDto(PayrollPeriod item)
        => new(item.PayrollPeriodId, item.Code, item.Name, item.StartDate, item.EndDate, item.PayDate, item.Status, item.Remarks, item.CreatedAt, item.UpdatedAt,
            item.ProcessingStartedAt, item.ClosedAt, item.CancelledAt, item.CancellationReason);

    private static bool IsUniqueCodeViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static ServiceResult<T> Validation<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private static ServiceResult<T> NotFound<T>() => ServiceResult<T>.Fail("not_found", "Payroll period was not found.");
}
