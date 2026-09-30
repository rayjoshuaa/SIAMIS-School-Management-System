using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeeCompensationService(SIAMISDbContext db) : IEmployeeCompensationService
{
    private const decimal MaximumSalary = 999999999999999.9999m;

    public async Task<ServiceResult<IReadOnlyList<EmployeeCompensationDto>>> GetCompensationsAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<IReadOnlyList<EmployeeCompensationDto>>("Employee was not found.");
        var compensations = await CompensationQuery()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CompensationId)
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeCompensationDto>>.Success(compensations);
    }

    public async Task<ServiceResult<EmployeeCompensationDto>> GetCompensationAsync(Guid employeeId, Guid compensationId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeCompensationDto>("Employee was not found.");
        var item = await CompensationQuery().SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId && x.CompensationId == compensationId, ct);
        return item is null
            ? NotFound<EmployeeCompensationDto>("Compensation was not found for this employee.")
            : ServiceResult<EmployeeCompensationDto>.Success(item);
    }

    public async Task<ServiceResult<EmployeeCompensationDto>> CreateCompensationAsync(Guid employeeId, EmployeeCompensationRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeCompensationDto>("Employee was not found.");
        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeeCompensationDto>(validation);

        var current = request.IsCurrent == true
            ? await db.EmployeeCompensations.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.IsCurrent, ct)
            : null;
        if (current is not null)
        {
            validation = ValidateCurrentReplacement(current, request.EffectiveFrom!.Value);
            if (validation is not null) return Invalid<EmployeeCompensationDto>(validation);
            current.IsCurrent = false;
            if (!current.EffectiveTo.HasValue) current.EffectiveTo = request.EffectiveFrom.Value.AddDays(-1);
            await db.SaveChangesAsync(ct);
        }

        var compensation = CreateEntity(employeeId, request);
        db.EmployeeCompensations.Add(compensation);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict<EmployeeCompensationDto>("Another current compensation was created concurrently. Retry the request.");
        }

        return await GetCompensationAsync(employeeId, compensation.EmployeeCompensationId, ct);
    }

    public async Task<ServiceResult<EmployeeCompensationDto>> UpdateCompensationAsync(Guid employeeId, Guid compensationId, EmployeeCompensationRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeeCompensationDto>("Employee was not found.");
        var compensation = await db.EmployeeCompensations.SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId && x.EmployeeCompensationId == compensationId, ct);
        if (compensation is null) return NotFound<EmployeeCompensationDto>("Compensation was not found for this employee.");

        var validation = await ValidateRequest(request, ct);
        if (validation is not null) return Invalid<EmployeeCompensationDto>(validation);

        if (request.IsCurrent == true && !compensation.IsCurrent)
        {
            var existingCurrent = await db.EmployeeCompensations.SingleOrDefaultAsync(
                x => x.EmployeeId == employeeId && x.IsCurrent && x.EmployeeCompensationId != compensationId, ct);
            if (existingCurrent is not null)
            {
                validation = ValidateCurrentReplacement(existingCurrent, request.EffectiveFrom!.Value);
                if (validation is not null) return Invalid<EmployeeCompensationDto>(validation);
                existingCurrent.IsCurrent = false;
                if (!existingCurrent.EffectiveTo.HasValue) existingCurrent.EffectiveTo = request.EffectiveFrom.Value.AddDays(-1);
                // Demote first to satisfy the filtered unique index before making the selected row current.
                await db.SaveChangesAsync(ct);
            }
        }

        ApplyRequest(compensation, request);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict<EmployeeCompensationDto>("Another current compensation was created concurrently. Retry the request.");
        }

        return await GetCompensationAsync(employeeId, compensationId, ct);
    }

    public async Task<ServiceResult<bool>> DeleteCompensationAsync(Guid employeeId, Guid compensationId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var compensation = await db.EmployeeCompensations.SingleOrDefaultAsync(
            x => x.EmployeeId == employeeId && x.EmployeeCompensationId == compensationId, ct);
        if (compensation is null) return NotFound<bool>("Compensation was not found for this employee.");
        db.EmployeeCompensations.Remove(compensation);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<string?> ValidateRequest(EmployeeCompensationRequest request, CancellationToken ct)
    {
        if (!request.PayTypeId.HasValue || !await db.PayTypes.AsNoTracking().AnyAsync(x => x.Id == request.PayTypeId && x.IsActive, ct))
            return "PayTypeId must reference an active pay type.";
        if (!request.BasicSalary.HasValue || request.BasicSalary.Value < 0 || request.BasicSalary.Value > MaximumSalary)
            return "BasicSalary must be non-negative and within the configured decimal(19,4) range.";
        if (decimal.Round(request.BasicSalary.Value, 4) != request.BasicSalary.Value)
            return "BasicSalary cannot have more than four decimal places.";
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
            return "Currency must contain exactly three characters.";
        if (!request.EffectiveFrom.HasValue) return "EffectiveFrom is required.";
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom.Value)
            return "EffectiveTo cannot be before EffectiveFrom.";
        if (!request.IsCurrent.HasValue) return "IsCurrent is required.";
        if (request.Remarks?.Length > 2000) return "Remarks cannot exceed 2000 characters.";
        return null;
    }

    private static string? ValidateCurrentReplacement(EmployeeCompensation current, DateOnly newEffectiveFrom)
    {
        if (newEffectiveFrom < current.EffectiveFrom)
            return "A new current compensation cannot start before the existing current compensation.";
        if (current.EffectiveTo.HasValue && current.EffectiveTo.Value >= newEffectiveFrom)
            return "The new current compensation would overlap the existing compensation period.";
        if (!current.EffectiveTo.HasValue && newEffectiveFrom == DateOnly.MinValue)
            return "The current compensation cannot be closed before the minimum supported date.";
        return null;
    }

    private static EmployeeCompensation CreateEntity(Guid employeeId, EmployeeCompensationRequest request)
    {
        var item = new EmployeeCompensation { EmployeeId = employeeId };
        ApplyRequest(item, request);
        return item;
    }

    private static void ApplyRequest(EmployeeCompensation item, EmployeeCompensationRequest request)
    {
        item.PayTypeId = request.PayTypeId;
        item.BasicSalary = request.BasicSalary!.Value;
        item.Currency = request.Currency.Trim();
        item.EffectiveFrom = request.EffectiveFrom!.Value;
        item.EffectiveTo = request.EffectiveTo;
        item.IsCurrent = request.IsCurrent!.Value;
        item.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
    }

    private IQueryable<EmployeeCompensationDto> CompensationQuery() => db.EmployeeCompensations.AsNoTracking().Select(x => new EmployeeCompensationDto
    {
        CompensationId = x.EmployeeCompensationId,
        EmployeeId = x.EmployeeId,
        PayTypeId = x.PayTypeId,
        PayTypeCode = x.PayType == null ? null : x.PayType.Code,
        PayTypeName = x.PayType == null ? null : x.PayType.Name,
        BasicSalary = x.BasicSalary,
        Currency = x.Currency.Trim(),
        EffectiveFrom = x.EffectiveFrom,
        EffectiveTo = x.EffectiveTo,
        IsCurrent = x.IsCurrent,
        Remarks = x.Remarks
    });

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct) => db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employeeId, ct);
    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
}
