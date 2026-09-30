using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.MasterData;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class PayrollComponentService(SIAMISDbContext db) : IPayrollComponentService
{
    public async Task<ServiceResult<IReadOnlyList<PayrollComponentDto>>> GetPayrollComponentsAsync(
        string? componentType, bool includeInactive, string? search, CancellationToken cancellationToken)
    {
        var canonicalType = NormalizeType(componentType);
        if (componentType is not null && canonicalType is null)
            return Validation<IReadOnlyList<PayrollComponentDto>>("ComponentType must be Earning or Deduction.");

        IQueryable<PayrollComponent> query = db.PayrollComponents.AsNoTracking();
        if (!includeInactive)
            query = query.Where(item => item.IsActive);
        if (canonicalType is not null)
            query = query.Where(item => item.Category == canonicalType);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term)
                || (item.Code != null && item.Code.Contains(term))
                || (item.Description != null && item.Description.Contains(term)));
        }

        IReadOnlyList<PayrollComponentDto> items = await query
            .OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Select(item => new PayrollComponentDto(item.Id, item.Code!, item.Name, item.Category, item.Description, item.CalculationMethod, item.PercentageBase, item.IsActive))
            .ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<PayrollComponentDto>>.Success(items);
    }

    public async Task<PayrollComponentDto?> GetPayrollComponentAsync(Guid id, CancellationToken cancellationToken)
        => await db.PayrollComponents.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new PayrollComponentDto(item.Id, item.Code!, item.Name, item.Category, item.Description, item.CalculationMethod, item.PercentageBase, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ServiceResult<PayrollComponentDto>> CreatePayrollComponentAsync(PayrollComponentRequest request, CancellationToken cancellationToken)
    {
        var values = Normalize(request);
        if (values.Failure is not null) return ServiceResult<PayrollComponentDto>.Fail(values.Failure.Code, values.Failure.Message);
        if (await db.PayrollComponents.AnyAsync(item => item.Code == values.Code, cancellationToken))
            return Conflict<PayrollComponentDto>("A payroll component with this code already exists.");

        var component = new PayrollComponent
        {
            Code = values.Code,
            Name = values.Name!,
            Category = values.Type!,
            CalculationMethod = values.CalculationMethod!,
            PercentageBase = values.PercentageBase,
            Description = values.Description,
            IsActive = true
        };
        db.PayrollComponents.Add(component);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict<PayrollComponentDto>("A payroll component with this code already exists.");
        }
        return ServiceResult<PayrollComponentDto>.Success(Map(component));
    }

    public async Task<ServiceResult<PayrollComponentDto>> UpdatePayrollComponentAsync(Guid id, PayrollComponentRequest request, CancellationToken cancellationToken)
    {
        var component = await db.PayrollComponents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (component is null) return NotFound<PayrollComponentDto>();
        var values = Normalize(request);
        if (values.Failure is not null) return ServiceResult<PayrollComponentDto>.Fail(values.Failure.Code, values.Failure.Message);
        if (await db.PayrollComponents.AnyAsync(item => item.Id != id && item.Code == values.Code, cancellationToken))
            return Conflict<PayrollComponentDto>("A payroll component with this code already exists.");

        component.Code = values.Code;
        component.Name = values.Name!;
        component.Category = values.Type!;
        component.CalculationMethod = values.CalculationMethod!;
        component.PercentageBase = values.PercentageBase;
        component.Description = values.Description;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict<PayrollComponentDto>("A payroll component with this code already exists.");
        }
        return ServiceResult<PayrollComponentDto>.Success(Map(component));
    }

    public async Task<ServiceResult<bool>> SetPayrollComponentStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var component = await db.PayrollComponents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (component is null) return NotFound<bool>();
        component.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeletePayrollComponentAsync(Guid id, CancellationToken cancellationToken)
    {
        var component = await db.PayrollComponents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (component is null) return NotFound<bool>();
        db.PayrollComponents.Remove(component);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private static (string? Code, string? Name, string? Type, string? CalculationMethod, string? PercentageBase, string? Description, ApiFailure? Failure) Normalize(PayrollComponentRequest request)
    {
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        var type = NormalizeType(request.ComponentType);
        var calculationMethod = NormalizeCalculationMethod(request.CalculationMethod);
        var percentageBase = NormalizePercentageBase(request.PercentageBase);
        if (string.IsNullOrWhiteSpace(code)) return (null, null, null, null, null, null, new("validation", "Code is required."));
        if (string.IsNullOrWhiteSpace(name)) return (null, null, null, null, null, null, new("validation", "Name is required."));
        if (type is null) return (null, null, null, null, null, null, new("validation", "ComponentType must be Earning or Deduction."));
        if (calculationMethod is null) return (null, null, null, null, null, null, new("validation", "CalculationMethod must be FixedAmount, QuantityRate, Percentage, or Manual."));
        if (calculationMethod == "Percentage" && percentageBase is null)
            return (null, null, null, null, null, null, new("validation", "PercentageBase is required when CalculationMethod is Percentage and must be BasicSalary, GrossEarnings, or GrossPay."));
        if (calculationMethod != "Percentage" && request.PercentageBase is not null)
            return (null, null, null, null, null, null, new("validation", "PercentageBase must be null unless CalculationMethod is Percentage."));
        return (code, name, type, calculationMethod, percentageBase, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), null);
    }

    private static string? NormalizeCalculationMethod(string? value)
        => value?.Trim() switch
        {
            { } method when method.Equals("FixedAmount", StringComparison.OrdinalIgnoreCase) => "FixedAmount",
            { } method when method.Equals("QuantityRate", StringComparison.OrdinalIgnoreCase) => "QuantityRate",
            { } method when method.Equals("Percentage", StringComparison.OrdinalIgnoreCase) => "Percentage",
            { } method when method.Equals("Manual", StringComparison.OrdinalIgnoreCase) => "Manual",
            _ => null
        };

    private static string? NormalizePercentageBase(string? value)
        => value?.Trim() switch
        {
            { } baseValue when baseValue.Equals("BasicSalary", StringComparison.OrdinalIgnoreCase) => "BasicSalary",
            { } baseValue when baseValue.Equals("GrossEarnings", StringComparison.OrdinalIgnoreCase) => "GrossEarnings",
            { } baseValue when baseValue.Equals("GrossPay", StringComparison.OrdinalIgnoreCase) => "GrossPay",
            _ => null
        };

    private static string? NormalizeType(string? value)
        => value?.Trim().Equals("Earning", StringComparison.OrdinalIgnoreCase) == true ? "Earning"
            : value?.Trim().Equals("Deduction", StringComparison.OrdinalIgnoreCase) == true ? "Deduction"
            : null;

    private static PayrollComponentDto Map(PayrollComponent item)
        => new(item.Id, item.Code!, item.Name, item.Category, item.Description, item.CalculationMethod, item.PercentageBase, item.IsActive);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static ServiceResult<T> Validation<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private static ServiceResult<T> NotFound<T>() => ServiceResult<T>.Fail("not_found", "Payroll component was not found.");
}
