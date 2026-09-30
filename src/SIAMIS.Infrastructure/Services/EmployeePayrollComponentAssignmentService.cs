using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class EmployeePayrollComponentAssignmentService(SIAMISDbContext db) : IEmployeePayrollComponentAssignmentService
{
    private const decimal MaximumAmount = 999_999_999_999_999.9999m;

    public async Task<ServiceResult<IReadOnlyList<EmployeePayrollComponentAssignmentDto>>> GetEmployeeAssignmentsAsync(
        Guid employeeId, Guid? payrollComponentId, DateOnly? activeOn, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct))
            return NotFound<IReadOnlyList<EmployeePayrollComponentAssignmentDto>>("Employee was not found.");

        var query = db.EmployeePayrollComponentAssignments.AsNoTracking().Where(item => item.EmployeeId == employeeId);
        if (payrollComponentId.HasValue)
            query = query.Where(item => item.PayrollComponentId == payrollComponentId.Value);
        if (activeOn.HasValue)
            query = query.Where(item => item.EffectiveFrom <= activeOn.Value
                && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= activeOn.Value));

        IReadOnlyList<EmployeePayrollComponentAssignmentDto> assignments = await query
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenBy(item => item.PayrollComponent.Name)
            .ThenBy(item => item.PayrollComponent.Code)
            .Select(ToDtoProjection())
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeePayrollComponentAssignmentDto>>.Success(assignments);
    }

    public async Task<ServiceResult<EmployeePayrollComponentAssignmentDto>> GetEmployeeAssignmentAsync(Guid employeeId, Guid assignmentId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeePayrollComponentAssignmentDto>("Employee was not found.");
        var assignment = await db.EmployeePayrollComponentAssignments.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId && item.EmployeePayrollComponentAssignmentId == assignmentId)
            .Select(ToDtoProjection()).SingleOrDefaultAsync(ct);
        return assignment is null
            ? NotFound<EmployeePayrollComponentAssignmentDto>("Payroll component assignment was not found for this employee.")
            : ServiceResult<EmployeePayrollComponentAssignmentDto>.Success(assignment);
    }

    public async Task<ServiceResult<EmployeePayrollComponentAssignmentDto>> CreateEmployeeAssignmentAsync(
        Guid employeeId, EmployeePayrollComponentAssignmentRequest request, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await EmployeeExists(employeeId, ct))
                return NotFound<EmployeePayrollComponentAssignmentDto>("Employee was not found.");
            var validation = ValidateRequest(request);
            if (validation is not null) return Invalid<EmployeePayrollComponentAssignmentDto>(validation);

            var component = await db.PayrollComponents.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.PayrollComponentId!.Value, ct);
            if (component is null) return NotFound<EmployeePayrollComponentAssignmentDto>("Payroll component was not found.");
            if (!component.IsActive) return Invalid<EmployeePayrollComponentAssignmentDto>("Payroll component must be active.");
            if (await HasOverlap(employeeId, component.Id, request.EffectiveFrom!.Value, request.EffectiveTo, null, ct))
                return Conflict<EmployeePayrollComponentAssignmentDto>("This employee already has an overlapping assignment for this payroll component.");

            var assignment = new EmployeePayrollComponentAssignment { EmployeeId = employeeId };
            ApplyRequest(assignment, request, component.Id);
            db.EmployeePayrollComponentAssignments.Add(assignment);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await GetEmployeeAssignmentAsync(employeeId, assignment.EmployeePayrollComponentAssignmentId, ct);
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return Conflict<EmployeePayrollComponentAssignmentDto>("The assignment overlaps another concurrent change. Retry the request.");
        }
    }

    public async Task<ServiceResult<EmployeePayrollComponentAssignmentDto>> UpdateEmployeeAssignmentAsync(
        Guid employeeId, Guid assignmentId, EmployeePayrollComponentAssignmentRequest request, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await EmployeeExists(employeeId, ct)) return NotFound<EmployeePayrollComponentAssignmentDto>("Employee was not found.");
            var assignment = await db.EmployeePayrollComponentAssignments.SingleOrDefaultAsync(item =>
                item.EmployeeId == employeeId && item.EmployeePayrollComponentAssignmentId == assignmentId, ct);
            if (assignment is null) return NotFound<EmployeePayrollComponentAssignmentDto>("Payroll component assignment was not found for this employee.");
            var validation = ValidateRequest(request);
            if (validation is not null) return Invalid<EmployeePayrollComponentAssignmentDto>(validation);

            var component = await db.PayrollComponents.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.PayrollComponentId!.Value, ct);
            if (component is null) return NotFound<EmployeePayrollComponentAssignmentDto>("Payroll component was not found.");
            if (!component.IsActive) return Invalid<EmployeePayrollComponentAssignmentDto>("Payroll component must be active.");
            if (await HasOverlap(employeeId, component.Id, request.EffectiveFrom!.Value, request.EffectiveTo, assignmentId, ct))
                return Conflict<EmployeePayrollComponentAssignmentDto>("This employee already has an overlapping assignment for this payroll component.");

            ApplyRequest(assignment, request, component.Id);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await GetEmployeeAssignmentAsync(employeeId, assignmentId, ct);
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return Conflict<EmployeePayrollComponentAssignmentDto>("The assignment overlaps another concurrent change. Retry the request.");
        }
    }

    public async Task<ServiceResult<bool>> DeleteEmployeeAssignmentAsync(Guid employeeId, Guid assignmentId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return NotFound<bool>("Employee was not found.");
        var assignment = await db.EmployeePayrollComponentAssignments.SingleOrDefaultAsync(item =>
            item.EmployeeId == employeeId && item.EmployeePayrollComponentAssignmentId == assignmentId, ct);
        if (assignment is null) return NotFound<bool>("Payroll component assignment was not found for this employee.");
        db.EmployeePayrollComponentAssignments.Remove(assignment);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<PagedResult<EmployeePayrollComponentAssignmentListItemDto>>> GetAssignmentsAsync(
        EmployeePayrollComponentAssignmentListQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            return Invalid<PagedResult<EmployeePayrollComponentAssignmentListItemDto>>("Page must be positive and PageSize must be between 1 and 100.");

        var source = db.EmployeePayrollComponentAssignments.AsNoTracking().AsQueryable();
        if (query.EmployeeId.HasValue) source = source.Where(item => item.EmployeeId == query.EmployeeId.Value);
        if (query.PayrollComponentId.HasValue) source = source.Where(item => item.PayrollComponentId == query.PayrollComponentId.Value);
        if (query.ActiveOn.HasValue)
            source = source.Where(item => item.EffectiveFrom <= query.ActiveOn.Value
                && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= query.ActiveOn.Value));

        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(item => item.EffectiveFrom)
            .ThenBy(item => item.Employee.EmployeeNumber).ThenBy(item => item.PayrollComponent.Name)
            .ThenBy(item => item.EmployeePayrollComponentAssignmentId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => new EmployeePayrollComponentAssignmentListItemDto(
                item.EmployeePayrollComponentAssignmentId,
                item.EmployeeId,
                item.Employee.EmployeeNumber,
                item.Employee.FirstName,
                item.Employee.LastName,
                item.PayrollComponentId,
                item.PayrollComponent.Code,
                item.PayrollComponent.Name,
                item.PayrollComponent.Category,
                item.PayrollComponent.CalculationMethod,
                item.PayrollComponent.PercentageBase,
                item.PayrollComponent.IsTaxable,
                item.PayrollComponent.IsStatutory,
                item.PayrollComponent.ContributionSide,
                item.Amount,
                item.Quantity,
                item.Rate,
                item.EffectiveFrom,
                item.EffectiveTo,
                item.Remarks,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(ct);
        return ServiceResult<PagedResult<EmployeePayrollComponentAssignmentListItemDto>>.Success(new(items, query.Page, query.PageSize, total));
    }

    private Task<bool> HasOverlap(Guid employeeId, Guid componentId, DateOnly effectiveFrom, DateOnly? effectiveTo,
        Guid? excludedAssignmentId, CancellationToken ct)
    {
        var end = effectiveTo ?? DateOnly.MaxValue;
        return db.EmployeePayrollComponentAssignments.AnyAsync(item =>
            item.EmployeeId == employeeId && item.PayrollComponentId == componentId
            && (excludedAssignmentId == null || item.EmployeePayrollComponentAssignmentId != excludedAssignmentId.Value)
            && item.EffectiveFrom <= end
            && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= effectiveFrom), ct);
    }

    private static string? ValidateRequest(EmployeePayrollComponentAssignmentRequest request)
    {
        if (!request.PayrollComponentId.HasValue) return "PayrollComponentId is required.";
        if (!request.Amount.HasValue || !ValidDecimal(request.Amount.Value)) return "Amount must be non-negative and fit decimal(19,4).";
        if (request.Quantity.HasValue && !ValidDecimal(request.Quantity.Value)) return "Quantity must be non-negative and fit decimal(19,4).";
        if (request.Rate.HasValue && !ValidDecimal(request.Rate.Value)) return "Rate must be non-negative and fit decimal(19,4).";
        if (!request.EffectiveFrom.HasValue) return "EffectiveFrom is required.";
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom.Value)
            return "EffectiveTo cannot be before EffectiveFrom.";
        if (request.Remarks?.Length > 1000) return "Remarks cannot exceed 1000 characters.";
        return null;
    }

    private static bool ValidDecimal(decimal value) => value >= 0 && value <= MaximumAmount && decimal.Round(value, 4) == value;

    private static void ApplyRequest(EmployeePayrollComponentAssignment assignment,
        EmployeePayrollComponentAssignmentRequest request, Guid payrollComponentId)
    {
        assignment.PayrollComponentId = payrollComponentId;
        assignment.Amount = request.Amount!.Value;
        assignment.Quantity = request.Quantity;
        assignment.Rate = request.Rate;
        assignment.EffectiveFrom = request.EffectiveFrom!.Value;
        assignment.EffectiveTo = request.EffectiveTo;
        assignment.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
    }

    private static System.Linq.Expressions.Expression<Func<EmployeePayrollComponentAssignment, EmployeePayrollComponentAssignmentDto>> ToDtoProjection()
        => item => new EmployeePayrollComponentAssignmentDto(
            item.EmployeePayrollComponentAssignmentId,
            item.EmployeeId,
            item.PayrollComponentId,
            new PayrollComponentAssignmentComponentDto(item.PayrollComponent.Id, item.PayrollComponent.Code,
                item.PayrollComponent.Name, item.PayrollComponent.Category, item.PayrollComponent.CalculationMethod,
                item.PayrollComponent.PercentageBase, item.PayrollComponent.IsActive, item.PayrollComponent.IsTaxable,
                item.PayrollComponent.IsStatutory, item.PayrollComponent.ContributionSide),
            item.Amount,
            item.Quantity,
            item.Rate,
            item.EffectiveFrom,
            item.EffectiveTo,
            item.Remarks,
            item.CreatedAt,
            item.UpdatedAt);

    private Task<bool> EmployeeExists(Guid employeeId, CancellationToken ct)
        => db.Employees.AsNoTracking().AnyAsync(item => item.EmployeeId == employeeId, ct);

    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
}
