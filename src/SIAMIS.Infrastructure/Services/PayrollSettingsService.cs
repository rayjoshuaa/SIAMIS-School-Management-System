using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class PayrollSettingsService(SIAMISDbContext db) : IPayrollSettingsService
{
    private static readonly string[] PayFrequencies = ["Monthly", "SemiMonthly", "BiWeekly", "Weekly", "Daily", "Other"];
    private static readonly string[] RoundingModes = ["None", "Up", "Down", "Nearest"];

    public async Task<PayrollSettingsDto?> GetCurrentPayrollSettingsAsync(CancellationToken cancellationToken)
        => await db.PayrollSettings.AsNoTracking().Where(item => item.IsActive)
            .Select(item => new PayrollSettingsDto(item.PayrollSettingsId, item.Currency, item.PayFrequency,
                item.PayrollCutoffDay, item.DefaultPayDay, item.WorkingDaysPerPeriod, item.WorkingHoursPerDay,
                item.RoundingMode, item.DecimalPlaces, item.IsActive, item.CreatedAt, item.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PayrollSettingsDto?> GetPayrollSettingsAsync(Guid payrollSettingsId, CancellationToken cancellationToken)
        => await db.PayrollSettings.AsNoTracking().Where(item => item.PayrollSettingsId == payrollSettingsId)
            .Select(item => new PayrollSettingsDto(item.PayrollSettingsId, item.Currency, item.PayFrequency,
                item.PayrollCutoffDay, item.DefaultPayDay, item.WorkingDaysPerPeriod, item.WorkingHoursPerDay,
                item.RoundingMode, item.DecimalPlaces, item.IsActive, item.CreatedAt, item.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ServiceResult<PayrollSettingsDto>> CreatePayrollSettingsAsync(PayrollSettingsRequest request, CancellationToken cancellationToken)
    {
        var values = Normalize(request);
        if (values.Failure is not null) return Failure<PayrollSettingsDto>(values.Failure);
        var isActive = request.IsActive ?? true;
        if (isActive && await db.PayrollSettings.AsNoTracking().AnyAsync(item => item.IsActive, cancellationToken))
            return Conflict<PayrollSettingsDto>("An active Payroll Settings record already exists. Deactivate it before creating another active record.");

        var settings = new PayrollSettings();
        Apply(settings, values, isActive);
        db.PayrollSettings.Add(settings);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        { return Conflict<PayrollSettingsDto>("An active Payroll Settings record already exists. Deactivate it before creating another active record."); }
        return ServiceResult<PayrollSettingsDto>.Success(ToDto(settings));
    }

    public async Task<ServiceResult<PayrollSettingsDto>> UpdatePayrollSettingsAsync(Guid payrollSettingsId, PayrollSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await db.PayrollSettings.SingleOrDefaultAsync(item => item.PayrollSettingsId == payrollSettingsId, cancellationToken);
        if (settings is null) return NotFound<PayrollSettingsDto>();
        var values = Normalize(request);
        if (values.Failure is not null) return Failure<PayrollSettingsDto>(values.Failure);
        var isActive = request.IsActive ?? settings.IsActive;
        if (isActive && !settings.IsActive
            && await db.PayrollSettings.AsNoTracking().AnyAsync(item => item.IsActive && item.PayrollSettingsId != payrollSettingsId, cancellationToken))
            return Conflict<PayrollSettingsDto>("Another active Payroll Settings record already exists.");

        Apply(settings, values, isActive);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        { return Conflict<PayrollSettingsDto>("Another active Payroll Settings record already exists."); }
        return ServiceResult<PayrollSettingsDto>.Success(ToDto(settings));
    }

    public async Task<ServiceResult<bool>> SetPayrollSettingsStatusAsync(Guid payrollSettingsId, bool isActive, CancellationToken cancellationToken)
    {
        var settings = await db.PayrollSettings.SingleOrDefaultAsync(item => item.PayrollSettingsId == payrollSettingsId, cancellationToken);
        if (settings is null) return NotFound<bool>();
        if (isActive && !settings.IsActive
            && await db.PayrollSettings.AsNoTracking().AnyAsync(item => item.IsActive && item.PayrollSettingsId != payrollSettingsId, cancellationToken))
            return Conflict<bool>("Another active Payroll Settings record already exists.");
        settings.IsActive = isActive;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        { return Conflict<bool>("Another active Payroll Settings record already exists."); }
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeletePayrollSettingsAsync(Guid payrollSettingsId, CancellationToken cancellationToken)
    {
        var settings = await db.PayrollSettings.SingleOrDefaultAsync(item => item.PayrollSettingsId == payrollSettingsId, cancellationToken);
        if (settings is null) return NotFound<bool>();
        db.PayrollSettings.Remove(settings);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private static (string? Currency, string? PayFrequency, int? PayrollCutoffDay, int? DefaultPayDay,
        decimal? WorkingDaysPerPeriod, decimal? WorkingHoursPerDay, string? RoundingMode, int? DecimalPlaces, ApiFailure? Failure) Normalize(PayrollSettingsRequest request)
    {
        var currency = request.Currency?.Trim().ToUpperInvariant();
        var frequency = Canonical(request.PayFrequency, PayFrequencies);
        var rounding = Canonical(request.RoundingMode, RoundingModes);
        if (string.IsNullOrWhiteSpace(currency)) return Invalid("Currency is required.");
        if (currency.Length > 10) return Invalid("Currency cannot exceed 10 characters.");
        if (frequency is null) return Invalid("PayFrequency must be Monthly, SemiMonthly, BiWeekly, Weekly, Daily, or Other.");
        if (!request.PayrollCutoffDay.HasValue || request.PayrollCutoffDay is < 1 or > 31) return Invalid("PayrollCutoffDay must be between 1 and 31.");
        if (!request.DefaultPayDay.HasValue || request.DefaultPayDay is < 1 or > 31) return Invalid("DefaultPayDay must be between 1 and 31.");
        if (!request.WorkingDaysPerPeriod.HasValue || request.WorkingDaysPerPeriod is <= 0 or > 366 || decimal.Round(request.WorkingDaysPerPeriod.Value, 2) != request.WorkingDaysPerPeriod.Value)
            return Invalid("WorkingDaysPerPeriod must be positive, no greater than 366, and fit decimal(5,2).");
        if (!request.WorkingHoursPerDay.HasValue || request.WorkingHoursPerDay is <= 0 or > 24 || decimal.Round(request.WorkingHoursPerDay.Value, 2) != request.WorkingHoursPerDay.Value)
            return Invalid("WorkingHoursPerDay must be positive, no greater than 24, and fit decimal(5,2).");
        if (rounding is null) return Invalid("RoundingMode must be None, Up, Down, or Nearest.");
        if (!request.DecimalPlaces.HasValue || request.DecimalPlaces is < 0 or > 6) return Invalid("DecimalPlaces must be between 0 and 6.");
        return (currency, frequency, request.PayrollCutoffDay, request.DefaultPayDay, request.WorkingDaysPerPeriod,
            request.WorkingHoursPerDay, rounding, request.DecimalPlaces, null);
    }

    private static void Apply(PayrollSettings settings,
        (string? Currency, string? PayFrequency, int? PayrollCutoffDay, int? DefaultPayDay,
            decimal? WorkingDaysPerPeriod, decimal? WorkingHoursPerDay, string? RoundingMode, int? DecimalPlaces, ApiFailure? Failure) values,
        bool isActive)
    {
        settings.Currency = values.Currency!;
        settings.PayFrequency = values.PayFrequency!;
        settings.PayrollCutoffDay = values.PayrollCutoffDay!.Value;
        settings.DefaultPayDay = values.DefaultPayDay!.Value;
        settings.WorkingDaysPerPeriod = values.WorkingDaysPerPeriod!.Value;
        settings.WorkingHoursPerDay = values.WorkingHoursPerDay!.Value;
        settings.RoundingMode = values.RoundingMode!;
        settings.DecimalPlaces = values.DecimalPlaces!.Value;
        settings.IsActive = isActive;
    }

    private static string? Canonical(string? value, IReadOnlyList<string> values)
        => values.FirstOrDefault(item => item.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
    private static PayrollSettingsDto ToDto(PayrollSettings item)
        => new(item.PayrollSettingsId, item.Currency, item.PayFrequency, item.PayrollCutoffDay, item.DefaultPayDay,
            item.WorkingDaysPerPeriod, item.WorkingHoursPerDay, item.RoundingMode, item.DecimalPlaces, item.IsActive,
            item.CreatedAt, item.UpdatedAt);
    private static (string? Currency, string? PayFrequency, int? PayrollCutoffDay, int? DefaultPayDay,
        decimal? WorkingDaysPerPeriod, decimal? WorkingHoursPerDay, string? RoundingMode, int? DecimalPlaces, ApiFailure? Failure) Invalid(string message)
        => (null, null, null, null, null, null, null, null, new("validation", message));
    private static ServiceResult<T> Failure<T>(ApiFailure failure) => ServiceResult<T>.Fail(failure.Code, failure.Message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private static ServiceResult<T> NotFound<T>() => ServiceResult<T>.Fail("not_found", "Payroll Settings record was not found.");
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
