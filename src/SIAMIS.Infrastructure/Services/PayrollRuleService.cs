using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

public sealed class PayrollRuleService(SIAMISDbContext db) : IPayrollRuleService
{
    private const decimal MaximumDecimal19Scale4 = 999_999_999_999_999.9999m;
    private static readonly string[] RuleTypes = ["Statutory", "EmployerBenefit", "EmployeeBenefit", "Deduction", "Other"];
    private static readonly string[] CalculationMethods = ["Percentage", "FixedAmount", "Manual"];
    private static readonly string[] CalculationStages = ["Earning", "Deduction"];
    private static readonly string[] BaseTypes = ["BasicSalary", "GrossEarnings", "GrossPay", "TaxableIncome", "Custom"];
    private static readonly string[] AppliesToValues = ["Employee", "Employer", "Both"];

    public async Task<ServiceResult<PagedResult<PayrollRuleDto>>> GetPayrollRulesAsync(PayrollRuleListQuery query, CancellationToken cancellationToken)
    {
        var ruleType = Normalize(query.RuleType, RuleTypes);
        if (query.RuleType is not null && ruleType is null) return Invalid<PagedResult<PayrollRuleDto>>("RuleType is not supported.");
        var method = Normalize(query.CalculationMethod, CalculationMethods);
        if (query.CalculationMethod is not null && method is null) return Invalid<PagedResult<PayrollRuleDto>>("CalculationMethod is not supported.");
        var stage = NormalizeCalculationStage(query.CalculationStage);
        if (query.CalculationStage is not null && stage is null) return Invalid<PagedResult<PayrollRuleDto>>("CalculationStage must be Earning or Deduction.");
        var appliesTo = Normalize(query.AppliesTo, AppliesToValues);
        if (query.AppliesTo is not null && appliesTo is null) return Invalid<PagedResult<PayrollRuleDto>>("AppliesTo is not supported.");
        if (query.Page < 1 || query.PageSize is < 1 or > 100) return Invalid<PagedResult<PayrollRuleDto>>("Page must be positive and PageSize must be between 1 and 100.");

        IQueryable<PayrollRule> rules = db.PayrollRules.AsNoTracking();
        rules = rules.Where(item => item.IsActive == (query.IsActive ?? true));
        if (ruleType is not null) rules = rules.Where(item => item.RuleType == ruleType);
        if (method is not null) rules = rules.Where(item => item.CalculationMethod == method);
        if (stage is not null) rules = rules.Where(item => item.CalculationStage == stage);
        if (appliesTo is not null) rules = rules.Where(item => item.AppliesTo == appliesTo);
        if (query.ActiveOn.HasValue)
        {
            var date = query.ActiveOn.Value;
            rules = rules.Where(item => item.EffectiveFrom <= date && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= date));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rules = rules.Where(item => item.Code.Contains(term) || item.Name.Contains(term)
                || (item.Description != null && item.Description.Contains(term)));
        }

        var totalCount = await rules.CountAsync(cancellationToken);
        var items = await rules.OrderBy(item => item.Priority).ThenBy(item => item.Name).ThenBy(item => item.PayrollRuleId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(item => ToDto(item)).ToListAsync(cancellationToken);
        return ServiceResult<PagedResult<PayrollRuleDto>>.Success(new(items, query.Page, query.PageSize, totalCount));
    }

    public async Task<PayrollRuleDto?> GetPayrollRuleAsync(Guid payrollRuleId, CancellationToken cancellationToken)
        => await db.PayrollRules.AsNoTracking().Where(item => item.PayrollRuleId == payrollRuleId)
            .Select(item => ToDto(item)).SingleOrDefaultAsync(cancellationToken);

    public async Task<ServiceResult<PayrollRuleDto>> CreatePayrollRuleAsync(PayrollRuleRequest request, CancellationToken cancellationToken)
    {
        var values = NormalizeRequest(request);
        if (values.Failure is not null) return Failure<PayrollRuleDto>(values.Failure);
        if (await db.PayrollRules.AnyAsync(item => item.Code == values.Code, cancellationToken))
            return Conflict<PayrollRuleDto>("A payroll rule with this Code already exists.");

        var rule = new PayrollRule();
        Apply(rule, values, request.IsActive ?? true);
        db.PayrollRules.Add(rule);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        { return Conflict<PayrollRuleDto>("A payroll rule with this Code already exists."); }
        return ServiceResult<PayrollRuleDto>.Success(ToDto(rule));
    }

    public async Task<ServiceResult<PayrollRuleDto>> UpdatePayrollRuleAsync(Guid payrollRuleId, PayrollRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await db.PayrollRules.SingleOrDefaultAsync(item => item.PayrollRuleId == payrollRuleId, cancellationToken);
        if (rule is null) return NotFound<PayrollRuleDto>();
        var values = NormalizeRequest(request);
        if (values.Failure is not null) return Failure<PayrollRuleDto>(values.Failure);
        if (await db.PayrollRules.AnyAsync(item => item.PayrollRuleId != payrollRuleId && item.Code == values.Code, cancellationToken))
            return Conflict<PayrollRuleDto>("A payroll rule with this Code already exists.");
        Apply(rule, values, request.IsActive ?? rule.IsActive);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        { return Conflict<PayrollRuleDto>("A payroll rule with this Code already exists."); }
        return ServiceResult<PayrollRuleDto>.Success(ToDto(rule));
    }

    public async Task<ServiceResult<bool>> SetPayrollRuleStatusAsync(Guid payrollRuleId, bool isActive, CancellationToken cancellationToken)
    {
        var rule = await db.PayrollRules.SingleOrDefaultAsync(item => item.PayrollRuleId == payrollRuleId, cancellationToken);
        if (rule is null) return NotFound<bool>();
        rule.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    public async Task<ServiceResult<bool>> DeletePayrollRuleAsync(Guid payrollRuleId, CancellationToken cancellationToken)
    {
        var rule = await db.PayrollRules.SingleOrDefaultAsync(item => item.PayrollRuleId == payrollRuleId, cancellationToken);
        if (rule is null) return NotFound<bool>();
        db.PayrollRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private static (string? Code, string? Name, int? Priority, string? Description, string? RuleType, string? CalculationMethod, string? CalculationStage,
        decimal? Rate, decimal? FixedAmount, decimal? MinimumBase, decimal? MaximumBase, string? BaseType,
        string? AppliesTo, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, ApiFailure? Failure) NormalizeRequest(PayrollRuleRequest request)
    {
        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        var ruleType = Normalize(request.RuleType, RuleTypes);
        var method = Normalize(request.CalculationMethod, CalculationMethods);
        var stage = NormalizeCalculationStage(request.CalculationStage);
        var baseType = request.BaseType is null ? null : Normalize(request.BaseType, BaseTypes);
        var appliesTo = Normalize(request.AppliesTo, AppliesToValues);

        if (string.IsNullOrWhiteSpace(code)) return InvalidValues("Code is required.");
        if (string.IsNullOrWhiteSpace(name)) return InvalidValues("Name is required.");
        if (!request.Priority.HasValue || request.Priority.Value < 0) return InvalidValues("Priority is required and must be greater than or equal to zero.");
        if (ruleType is null) return InvalidValues("RuleType must be Statutory, EmployerBenefit, EmployeeBenefit, Deduction, or Other.");
        if (method is null) return InvalidValues("CalculationMethod must be Percentage, FixedAmount, or Manual.");
        if (stage is null) return InvalidValues("CalculationStage must be Earning or Deduction.");
        if (request.BaseType is not null && baseType is null) return InvalidValues("BaseType must be BasicSalary, GrossEarnings, GrossPay, TaxableIncome, or Custom.");
        if (appliesTo is null) return InvalidValues("AppliesTo must be Employee, Employer, or Both.");
        if (!request.EffectiveFrom.HasValue) return InvalidValues("EffectiveFrom is required.");
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom.Value) return InvalidValues("EffectiveTo cannot be before EffectiveFrom.");
        foreach (var (field, value) in new[] { ("Rate", request.Rate), ("FixedAmount", request.FixedAmount), ("MinimumBase", request.MinimumBase), ("MaximumBase", request.MaximumBase) })
        {
            if (value < 0) return InvalidValues($"{field} must be greater than or equal to zero.");
            if (value > MaximumDecimal19Scale4 || (value.HasValue && decimal.Round(value.Value, 4) != value.Value))
                return InvalidValues($"{field} must fit decimal(19,4).");
        }
        if (request.MinimumBase.HasValue && request.MaximumBase.HasValue && request.MaximumBase.Value < request.MinimumBase.Value)
            return InvalidValues("MaximumBase cannot be less than MinimumBase.");
        if (method == "FixedAmount" && baseType is not null) return InvalidValues("BaseType must be null when CalculationMethod is FixedAmount.");
        if (method == "Manual" && (request.Rate.HasValue || request.FixedAmount.HasValue || baseType is not null))
            return InvalidValues("Manual rules cannot specify Rate, FixedAmount, or BaseType.");

        return (code, name, request.Priority, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), ruleType, method, stage,
            request.Rate, request.FixedAmount, request.MinimumBase, request.MaximumBase, baseType, appliesTo,
            request.EffectiveFrom, request.EffectiveTo, null);
    }

    private static void Apply(PayrollRule rule,
        (string? Code, string? Name, int? Priority, string? Description, string? RuleType, string? CalculationMethod, string? CalculationStage, decimal? Rate,
            decimal? FixedAmount, decimal? MinimumBase, decimal? MaximumBase, string? BaseType, string? AppliesTo,
            DateOnly? EffectiveFrom, DateOnly? EffectiveTo, ApiFailure? Failure) values, bool isActive)
    {
        rule.Code = values.Code!;
        rule.Name = values.Name!;
        rule.Priority = values.Priority!.Value;
        rule.Description = values.Description;
        rule.RuleType = values.RuleType!;
        rule.CalculationMethod = values.CalculationMethod!;
        rule.CalculationStage = values.CalculationStage!;
        rule.Rate = values.Rate;
        rule.FixedAmount = values.FixedAmount;
        rule.MinimumBase = values.MinimumBase;
        rule.MaximumBase = values.MaximumBase;
        rule.BaseType = values.BaseType;
        rule.AppliesTo = values.AppliesTo!;
        rule.EffectiveFrom = values.EffectiveFrom!.Value;
        rule.EffectiveTo = values.EffectiveTo;
        rule.IsActive = isActive;
    }

    private static string? Normalize(string? value, IReadOnlyList<string> allowed)
        => allowed.FirstOrDefault(item => item.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeCalculationStage(string? value)
        => string.Equals(value?.Trim(), "EARING", StringComparison.OrdinalIgnoreCase)
            ? "Earning"
            : Normalize(value, CalculationStages);

    private static PayrollRuleDto ToDto(PayrollRule item)
        => new(item.PayrollRuleId, item.Code, item.Name, item.Priority, item.Description, item.RuleType, item.CalculationMethod, item.CalculationStage,
            item.Rate, item.FixedAmount, item.MinimumBase, item.MaximumBase, item.BaseType, item.AppliesTo,
            item.EffectiveFrom, item.EffectiveTo, item.IsActive, item.CreatedAt, item.UpdatedAt);

    private static (string? Code, string? Name, int? Priority, string? Description, string? RuleType, string? CalculationMethod, string? CalculationStage,
        decimal? Rate, decimal? FixedAmount, decimal? MinimumBase, decimal? MaximumBase, string? BaseType,
        string? AppliesTo, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, ApiFailure? Failure) InvalidValues(string message)
        => (null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, new("validation", message));

    private static ServiceResult<T> Failure<T>(ApiFailure failure) => ServiceResult<T>.Fail(failure.Code, failure.Message);
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private static ServiceResult<T> NotFound<T>() => ServiceResult<T>.Fail("not_found", "Payroll rule was not found.");
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is SqlException { Number: 2601 or 2627 };
}
