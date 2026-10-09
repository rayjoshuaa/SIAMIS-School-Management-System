using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class PayrollRuleTargetService(SIAMISDbContext db) : IPayrollRuleTargetService
{
    private static readonly string[] TargetTypes = ["Employee", "Department", "Designation", "EmploymentType", "Location"];

    public async Task<ServiceResult<IReadOnlyList<PayrollRuleTargetDto>>> GetTargetsAsync(Guid payrollRuleId, CancellationToken cancellationToken)
    {
        if (!await db.PayrollRules.AsNoTracking().AnyAsync(item => item.PayrollRuleId == payrollRuleId, cancellationToken))
            return NotFound<IReadOnlyList<PayrollRuleTargetDto>>("Payroll rule was not found.");

        var targets = await db.PayrollRuleTargets.AsNoTracking().Where(item => item.PayrollRuleId == payrollRuleId)
            .ToListAsync(cancellationToken);
        var displays = await GetDisplaysAsync(targets, cancellationToken);
        IReadOnlyList<PayrollRuleTargetDto> result = targets
            .OrderBy(item => item.IsExcluded)
            .ThenBy(item => item.TargetType, StringComparer.Ordinal)
            .ThenBy(item => displays.GetValueOrDefault((item.TargetType, item.TargetId))?.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PayrollRuleTargetId)
            .Select(item => ToDto(item, displays.GetValueOrDefault((item.TargetType, item.TargetId))))
            .ToArray();
        return ServiceResult<IReadOnlyList<PayrollRuleTargetDto>>.Success(result);
    }

    public async Task<PayrollRuleTargetDto?> GetTargetAsync(Guid payrollRuleId, Guid payrollRuleTargetId, CancellationToken cancellationToken)
    {
        if (!await db.PayrollRules.AsNoTracking().AnyAsync(item => item.PayrollRuleId == payrollRuleId, cancellationToken)) return null;
        var target = await db.PayrollRuleTargets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PayrollRuleId == payrollRuleId && item.PayrollRuleTargetId == payrollRuleTargetId, cancellationToken);
        if (target is null) return null;
        var displays = await GetDisplaysAsync([target], cancellationToken);
        return ToDto(target, displays.GetValueOrDefault((target.TargetType, target.TargetId)));
    }

    public async Task<ServiceResult<PayrollRuleTargetDto>> CreateTargetAsync(Guid payrollRuleId, PayrollRuleTargetRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        if (!await db.PayrollRules.AnyAsync(item => item.PayrollRuleId == payrollRuleId, cancellationToken))
            return NotFound<PayrollRuleTargetDto>("Payroll rule was not found.");
        var values = await ValidateRequestAsync(request, cancellationToken);
        if (values.Failure is not null) return Failure<PayrollRuleTargetDto>(values.Failure);
        if (await HasDuplicateAsync(payrollRuleId, values.TargetType!, values.TargetId!.Value, null, cancellationToken))
            return Conflict<PayrollRuleTargetDto>("This target is already configured for the payroll rule, either as an inclusion or exclusion.");

        var target = new PayrollRuleTarget
        {
            PayrollRuleId = payrollRuleId,
            TargetType = values.TargetType!,
            TargetId = values.TargetId!.Value,
            IsExcluded = values.IsExcluded!.Value
        };
        db.PayrollRuleTargets.Add(target);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        { return Conflict<PayrollRuleTargetDto>("This target is already configured for the payroll rule, either as an inclusion or exclusion."); }
        await transaction.CommitAsync(cancellationToken);
        var displays = await GetDisplaysAsync([target], cancellationToken);
        return ServiceResult<PayrollRuleTargetDto>.Success(ToDto(target, displays.GetValueOrDefault((target.TargetType, target.TargetId))));
    }

    public async Task<ServiceResult<PayrollRuleTargetDto>> UpdateTargetAsync(Guid payrollRuleId, Guid payrollRuleTargetId,
        PayrollRuleTargetRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var target = await db.PayrollRuleTargets.SingleOrDefaultAsync(
            item => item.PayrollRuleId == payrollRuleId && item.PayrollRuleTargetId == payrollRuleTargetId, cancellationToken);
        if (target is null) return NotFound<PayrollRuleTargetDto>("Payroll rule target was not found.");
        var values = await ValidateRequestAsync(request, cancellationToken);
        if (values.Failure is not null) return Failure<PayrollRuleTargetDto>(values.Failure);
        if (await HasDuplicateAsync(payrollRuleId, values.TargetType!, values.TargetId!.Value, payrollRuleTargetId, cancellationToken))
            return Conflict<PayrollRuleTargetDto>("This target is already configured for the payroll rule, either as an inclusion or exclusion.");

        target.TargetType = values.TargetType!;
        target.TargetId = values.TargetId!.Value;
        target.IsExcluded = values.IsExcluded!.Value;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        { return Conflict<PayrollRuleTargetDto>("This target is already configured for the payroll rule, either as an inclusion or exclusion."); }
        await transaction.CommitAsync(cancellationToken);
        var displays = await GetDisplaysAsync([target], cancellationToken);
        return ServiceResult<PayrollRuleTargetDto>.Success(ToDto(target, displays.GetValueOrDefault((target.TargetType, target.TargetId))));
    }

    public async Task<ServiceResult<bool>> DeleteTargetAsync(Guid payrollRuleId, Guid payrollRuleTargetId, CancellationToken cancellationToken)
    {
        var target = await db.PayrollRuleTargets.SingleOrDefaultAsync(
            item => item.PayrollRuleId == payrollRuleId && item.PayrollRuleTargetId == payrollRuleTargetId, cancellationToken);
        if (target is null) return NotFound<bool>("Payroll rule target was not found.");
        db.PayrollRuleTargets.Remove(target);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private async Task<(string? TargetType, Guid? TargetId, bool? IsExcluded, ApiFailure? Failure)> ValidateRequestAsync(
        PayrollRuleTargetRequest request, CancellationToken cancellationToken)
    {
        var targetType = TargetTypes.FirstOrDefault(value => value.Equals(request.TargetType?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (targetType is null) return Invalid("TargetType must be Employee, Department, Designation, EmploymentType, or Location.");
        if (!request.TargetId.HasValue || request.TargetId.Value == Guid.Empty) return Invalid("TargetId must be a non-empty GUID.");
        if (!request.IsExcluded.HasValue) return Invalid("IsExcluded is required.");

        var id = request.TargetId.Value;
        switch (targetType)
        {
            case "Employee":
                if (await EmploymentIntegrity.LockAsync(db, id, cancellationToken) is null)
                    return Missing("Employee target was not found.");
                break;
            case "Department":
                var departmentActive = await db.Departments.AsNoTracking().Where(item => item.Id == id)
                    .Select(item => (bool?)item.IsActive).SingleOrDefaultAsync(cancellationToken);
                if (!departmentActive.HasValue) return Missing("Department target was not found.");
                if (!departmentActive.Value) return Invalid("Department targets must be active.");
                break;
            case "Designation":
                var designationActive = await db.Designations.AsNoTracking().Where(item => item.Id == id)
                    .Select(item => (bool?)item.IsActive).SingleOrDefaultAsync(cancellationToken);
                if (!designationActive.HasValue) return Missing("Designation target was not found.");
                if (!designationActive.Value) return Invalid("Designation targets must be active.");
                break;
            case "EmploymentType":
                var employmentTypeActive = await db.EmploymentTypes.AsNoTracking().Where(item => item.Id == id)
                    .Select(item => (bool?)item.IsActive).SingleOrDefaultAsync(cancellationToken);
                if (!employmentTypeActive.HasValue) return Missing("Employment Type target was not found.");
                if (!employmentTypeActive.Value) return Invalid("Employment Type targets must be active.");
                break;
            case "Location":
                var locationActive = await db.Locations.AsNoTracking().Where(item => item.Id == id)
                    .Select(item => (bool?)item.IsActive).SingleOrDefaultAsync(cancellationToken);
                if (!locationActive.HasValue) return Missing("Location target was not found.");
                if (!locationActive.Value) return Invalid("Location targets must be active.");
                break;
        }
        return (targetType, id, request.IsExcluded, null);
    }

    private Task<bool> HasDuplicateAsync(Guid payrollRuleId, string targetType, Guid targetId, Guid? exceptId, CancellationToken ct)
        => db.PayrollRuleTargets.AsNoTracking().AnyAsync(item => item.PayrollRuleId == payrollRuleId
            && item.TargetType == targetType && item.TargetId == targetId
            && (!exceptId.HasValue || item.PayrollRuleTargetId != exceptId.Value), ct);

    private async Task<Dictionary<(string Type, Guid Id), TargetDisplay>> GetDisplaysAsync(
        IReadOnlyCollection<PayrollRuleTarget> targets, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string Type, Guid Id), TargetDisplay>();
        var employees = targets.Where(item => item.TargetType == "Employee").Select(item => item.TargetId).Distinct().ToArray();
        if (employees.Length > 0)
        {
            var rows = await db.Employees.AsNoTracking().Where(item => employees.Contains(item.EmployeeId))
                .Select(item => new { item.EmployeeId, item.EmployeeNumber, item.FirstName, item.MiddleName, item.LastName, item.PreferredName })
                .ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                var name = string.Join(' ', new[] { string.IsNullOrWhiteSpace(row.PreferredName) ? row.FirstName : row.PreferredName, row.MiddleName, row.LastName }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
                result[("Employee", row.EmployeeId)] = new(name, row.EmployeeNumber);
            }
        }
        await AddMasterDisplaysAsync("Department", targets, result, cancellationToken);
        await AddMasterDisplaysAsync("Designation", targets, result, cancellationToken);
        await AddMasterDisplaysAsync("EmploymentType", targets, result, cancellationToken);
        await AddMasterDisplaysAsync("Location", targets, result, cancellationToken);
        return result;
    }

    private async Task AddMasterDisplaysAsync(string type, IReadOnlyCollection<PayrollRuleTarget> targets,
        Dictionary<(string Type, Guid Id), TargetDisplay> result, CancellationToken cancellationToken)
    {
        var ids = targets.Where(item => item.TargetType == type).Select(item => item.TargetId).Distinct().ToArray();
        if (ids.Length == 0) return;
        switch (type)
        {
            case "Department":
                foreach (var row in await db.Departments.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.Code }).ToListAsync(cancellationToken)) result[(type, row.Id)] = new(row.Name, row.Code);
                break;
            case "Designation":
                foreach (var row in await db.Designations.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.Code }).ToListAsync(cancellationToken)) result[(type, row.Id)] = new(row.Name, row.Code);
                break;
            case "EmploymentType":
                foreach (var row in await db.EmploymentTypes.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.Code }).ToListAsync(cancellationToken)) result[(type, row.Id)] = new(row.Name, row.Code);
                break;
            case "Location":
                foreach (var row in await db.Locations.AsNoTracking().Where(item => ids.Contains(item.Id)).Select(item => new { item.Id, item.Name, item.Code }).ToListAsync(cancellationToken)) result[(type, row.Id)] = new(row.Name, row.Code);
                break;
        }
    }

    private static PayrollRuleTargetDto ToDto(PayrollRuleTarget target, TargetDisplay? display)
        => new(target.PayrollRuleTargetId, target.PayrollRuleId, target.TargetType, target.TargetId,
            display?.Name, display?.Code, target.IsExcluded, target.CreatedAt);

    private static (string? TargetType, Guid? TargetId, bool? IsExcluded, ApiFailure? Failure) Invalid(string message)
        => (null, null, null, new("validation", message));
    private static (string? TargetType, Guid? TargetId, bool? IsExcluded, ApiFailure? Failure) Missing(string message)
        => (null, null, null, new("not_found", message));
    private static ServiceResult<T> Failure<T>(ApiFailure failure) => ServiceResult<T>.Fail(failure.Code, failure.Message);
    private static ServiceResult<T> NotFound<T>(string message) => ServiceResult<T>.Fail("not_found", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private sealed record TargetDisplay(string Name, string? Code);
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
