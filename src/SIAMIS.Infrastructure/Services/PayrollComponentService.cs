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
        string? componentType, bool includeInactive, string? search, bool? isTaxable, bool? isStatutory,
        string? contributionSide, CancellationToken cancellationToken)
    {
        var canonicalType = NormalizeType(componentType);
        if (componentType is not null && canonicalType is null)
            return Validation<IReadOnlyList<PayrollComponentDto>>("ComponentType must be Earning or Deduction.");
        var canonicalContributionSide = NormalizeContributionSide(contributionSide);
        if (!string.IsNullOrWhiteSpace(contributionSide) && canonicalContributionSide is null)
            return Validation<IReadOnlyList<PayrollComponentDto>>("ContributionSide must be Employee, Employer, or Both.");

        IQueryable<PayrollComponent> query = db.PayrollComponents.AsNoTracking();
        if (!includeInactive)
            query = query.Where(item => item.IsActive);
        if (canonicalType is not null)
            query = query.Where(item => item.Category == canonicalType);
        if (isTaxable.HasValue)
            query = query.Where(item => item.IsTaxable == isTaxable.Value);
        if (isStatutory.HasValue)
            query = query.Where(item => item.IsStatutory == isStatutory.Value);
        if (canonicalContributionSide is not null)
            query = query.Where(item => item.ContributionSide == canonicalContributionSide);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term)
                || (item.Code != null && item.Code.Contains(term))
                || (item.Description != null && item.Description.Contains(term)));
        }

        IReadOnlyList<PayrollComponentDto> items = await query
            .OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Select(item => new PayrollComponentDto(item.Id, item.Code!, item.Name, item.Category, item.Description, item.CalculationMethod,
                item.PercentageBase, item.IsActive, item.IsTaxable, item.IsStatutory, item.ContributionSide, item.SsoWageTreatment, item.PitIncomeTreatment, item.PitPaymentTreatment))
            .ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<PayrollComponentDto>>.Success(items);
    }

    public async Task<PayrollComponentDto?> GetPayrollComponentAsync(Guid id, CancellationToken cancellationToken)
        => await db.PayrollComponents.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new PayrollComponentDto(item.Id, item.Code!, item.Name, item.Category, item.Description, item.CalculationMethod,
                item.PercentageBase, item.IsActive, item.IsTaxable, item.IsStatutory, item.ContributionSide, item.SsoWageTreatment, item.PitIncomeTreatment, item.PitPaymentTreatment))
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
            IsTaxable = request.IsTaxable,
            IsStatutory = request.IsStatutory,
            ContributionSide = values.ContributionSide,
            SsoWageTreatment = request.SsoWageTreatment ?? "Unknown",
            PitIncomeTreatment = request.PitIncomeTreatment ?? "Unknown",
            PitPaymentTreatment = request.PitPaymentTreatment ?? "Unknown",
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
        component.IsTaxable = request.IsTaxable;
        component.IsStatutory = request.IsStatutory;
        component.ContributionSide = values.ContributionSide;
        component.SsoWageTreatment = request.SsoWageTreatment ?? component.SsoWageTreatment;
        component.PitIncomeTreatment = request.PitIncomeTreatment ?? component.PitIncomeTreatment;
        component.PitPaymentTreatment = request.PitPaymentTreatment ?? component.PitPaymentTreatment;
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

    private static (string? Code, string? Name, string? Type, string? CalculationMethod, string? PercentageBase,
        string? Description, string? ContributionSide, ApiFailure? Failure) Normalize(PayrollComponentRequest request)
    {
        if (request.PitPaymentTreatment is not (null or "Unknown" or "Regular" or "Special"))
            return (null, null, null, null, null, null, null, new("validation", "PitPaymentTreatment must be Unknown, Regular, or Special."));
        if (request.PitIncomeTreatment is not (null or "Unknown" or "Included" or "Excluded"))
            return (null, null, null, null, null, null, null, new("validation", "PitIncomeTreatment must be Unknown, Included, or Excluded."));
        if (request.SsoWageTreatment is not (null or "Unknown" or "Included" or "Excluded"))
            return (null, null, null, null, null, null, null, new("validation", "SsoWageTreatment must be Unknown, Included, or Excluded."));
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        var type = NormalizeType(request.ComponentType);
        var calculationMethod = NormalizeCalculationMethod(request.CalculationMethod);
        var percentageBase = NormalizePercentageBase(request.PercentageBase);
        var contributionSide = NormalizeContributionSide(request.ContributionSide);
        if (string.IsNullOrWhiteSpace(code)) return (null, null, null, null, null, null, null, new("validation", "Code is required."));
        if (string.IsNullOrWhiteSpace(name)) return (null, null, null, null, null, null, null, new("validation", "Name is required."));
        if (type is null) return (null, null, null, null, null, null, null, new("validation", "ComponentType must be Earning or Deduction."));
        if (calculationMethod is null) return (null, null, null, null, null, null, null, new("validation", "CalculationMethod must be FixedAmount, QuantityRate, Percentage, or Manual."));
        if (!string.IsNullOrWhiteSpace(request.ContributionSide) && contributionSide is null)
            return (null, null, null, null, null, null, null, new("validation", "ContributionSide must be Employee, Employer, or Both."));
        if (calculationMethod == "Percentage" && percentageBase is null)
            return (null, null, null, null, null, null, null, new("validation", "PercentageBase is required when CalculationMethod is Percentage and must be BasicSalary, GrossEarnings, or GrossPay."));
        if (calculationMethod != "Percentage" && request.PercentageBase is not null)
            return (null, null, null, null, null, null, null, new("validation", "PercentageBase must be null unless CalculationMethod is Percentage."));
        return (code, name, type, calculationMethod, percentageBase,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), contributionSide, null);
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

    private static string? NormalizeContributionSide(string? value)
        => value?.Trim() switch
        {
            { } side when side.Equals("Employee", StringComparison.OrdinalIgnoreCase) => "Employee",
            { } side when side.Equals("Employer", StringComparison.OrdinalIgnoreCase) => "Employer",
            { } side when side.Equals("Both", StringComparison.OrdinalIgnoreCase) => "Both",
            _ => null
        };

    private static PayrollComponentDto Map(PayrollComponent item)
        => new(item.Id, item.Code!, item.Name, item.Category, item.Description, item.CalculationMethod, item.PercentageBase,
            item.IsActive, item.IsTaxable, item.IsStatutory, item.ContributionSide, item.SsoWageTreatment, item.PitIncomeTreatment, item.PitPaymentTreatment);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static ServiceResult<T> Validation<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private static ServiceResult<T> NotFound<T>() => ServiceResult<T>.Fail("not_found", "Payroll component was not found.");
}
