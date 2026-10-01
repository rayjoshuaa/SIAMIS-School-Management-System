using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Resolves effective payroll rules for a single employee/period without calculating payroll amounts.</summary>
public sealed class PayrollRuleEvaluator(SIAMISDbContext db, IPayrollEmploymentContextService employmentContexts) : IPayrollRuleEvaluator
{
    private static readonly string[] SupportedAppliesTo = ["Employee", "Employer", "Both"];
    private static readonly string[] SupportedStages = ["Earning", "Deduction"];

    public Task<ServiceResult<PayrollRuleEvaluationDto>> EvaluateApplicableRulesAsync(
        Guid payrollPeriodId, Guid employeeId, CancellationToken cancellationToken)
        => EvaluateAsync(payrollPeriodId, employeeId, null, cancellationToken);

    public Task<ServiceResult<PayrollRuleEvaluationDto>> EvaluateApplicableRulesAsync(
        Guid payrollPeriodId, Guid employeeId, PayrollEmploymentContextDto employmentContext, CancellationToken cancellationToken)
        => EvaluateAsync(payrollPeriodId, employeeId, employmentContext, cancellationToken);

    private async Task<ServiceResult<PayrollRuleEvaluationDto>> EvaluateAsync(
        Guid payrollPeriodId, Guid employeeId, PayrollEmploymentContextDto? employmentRow, CancellationToken cancellationToken)
    {
        var period = await db.PayrollPeriods.AsNoTracking()
            .Where(item => item.PayrollPeriodId == payrollPeriodId)
            .Select(item => new PayrollRuleEvaluationPeriodDto(item.PayrollPeriodId, item.Code, item.Name,
                item.StartDate, item.EndDate, item.Status))
            .SingleOrDefaultAsync(cancellationToken);
        if (period is null) return NotFound("Payroll period was not found.");

        var employeeRow = await db.Employees.AsNoTracking().Where(item => item.EmployeeId == employeeId)
            .Select(item => new { item.EmployeeId, item.EmployeeNumber, item.FirstName, item.MiddleName, item.LastName, item.PreferredName, item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (employeeRow is null) return NotFound("Employee was not found.");

        if (employmentRow is null)
        {
            var contexts = await employmentContexts.ResolveAsync([employeeId], period.StartDate, period.EndDate, cancellationToken);
            var resolved = contexts[employeeId];
            if (!resolved.IsSuccess) return ServiceResult<PayrollRuleEvaluationDto>.Fail(resolved.Failure!.Code, resolved.Failure.Message);
            if (resolved.Value is null) return ServiceResult<PayrollRuleEvaluationDto>.Fail("validation", "Employee has no employment overlapping this payroll period and is not eligible.");
            employmentRow = resolved.Value;
        }

        var employeeName = string.Join(' ', new[]
        {
            string.IsNullOrWhiteSpace(employeeRow.PreferredName) ? employeeRow.FirstName : employeeRow.PreferredName,
            employeeRow.MiddleName,
            employeeRow.LastName
        }.Where(item => !string.IsNullOrWhiteSpace(item)));
        var employee = new PayrollRuleEvaluationEmployeeDto(employeeRow.EmployeeId, employeeRow.EmployeeNumber,
            employeeName, employeeRow.IsActive, new(
                employmentRow.DepartmentId, employmentRow.DepartmentName,
                employmentRow.DesignationId, employmentRow.DesignationName,
                employmentRow.EmploymentTypeId, employmentRow.EmploymentTypeName,
                employmentRow.LocationId, employmentRow.LocationName), employmentRow.TargetContextDate, employmentRow.EmploymentRecordId);
        var context = new EvaluationContext(employeeId,
            employmentRow.DepartmentId, employmentRow.DesignationId, employmentRow.EmploymentTypeId, employmentRow.LocationId);

        // Filter common eligibility in SQL, then evaluate target groups in memory from one batched target query.
        var candidateRules = await db.PayrollRules.AsNoTracking()
            .Where(item => item.IsActive && item.EffectiveFrom <= period.StartDate
                && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= period.StartDate))
            .OrderBy(item => item.CalculationStage == "Earning" ? 0 : 1)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.PayrollRuleId)
            .ToListAsync(cancellationToken);
        if (candidateRules.Count == 0)
            return ServiceResult<PayrollRuleEvaluationDto>.Success(new(employee, period, []));

        var ruleIds = candidateRules.Select(item => item.PayrollRuleId).ToArray();
        var targets = await db.PayrollRuleTargets.AsNoTracking()
            .Where(item => ruleIds.Contains(item.PayrollRuleId))
            .ToListAsync(cancellationToken);
        var targetsByRule = targets.GroupBy(item => item.PayrollRuleId).ToDictionary(group => group.Key, group => group.ToArray());

        var applicable = new List<ApplicableRuleCandidate>();
        // candidateRules are SQL-ordered by stage, priority, name, and ID; filtering preserves that deterministic order.
        foreach (var rule in candidateRules)
        {
            // AppliesTo is returned unchanged as metadata. Employee evaluation covers the employee's payroll context;
            // Employee, Employer, and Both remain distinct rule-side classifications for later calculation.
            if (!SupportedAppliesTo.Contains(rule.AppliesTo, StringComparer.OrdinalIgnoreCase)
                || !SupportedStages.Contains(rule.CalculationStage, StringComparer.OrdinalIgnoreCase))
                continue;

            var configuredTargets = targetsByRule.GetValueOrDefault(rule.PayrollRuleId) ?? [];
            var targetResult = EvaluateTargets(configuredTargets, context);
            if (targetResult.IsApplicable)
                applicable.Add(new(rule, targetResult.Summary, targetResult.Targets));
        }

        var displayTargets = applicable.SelectMany(item => item.Targets).Select(item => item.Target).ToArray();
        var displays = await ResolveTargetDisplaysAsync(displayTargets, cancellationToken);
        var componentIds = applicable.Select(item => item.Rule.PayrollComponentId).Distinct().ToArray();
        var componentDisplays = await db.PayrollComponents.AsNoTracking().Where(item => componentIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Code, item.Name, item.Category, item.IsTaxable, item.IsStatutory, item.ContributionSide, item.SsoWageTreatment })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var orderedRules = applicable
            .Select(item => new ApplicablePayrollRuleDto(
                item.Rule.PayrollRuleId, item.Rule.Code, item.Rule.Name,
                item.Rule.PayrollComponentId, componentDisplays[item.Rule.PayrollComponentId].Code,
                componentDisplays[item.Rule.PayrollComponentId].Name, componentDisplays[item.Rule.PayrollComponentId].Category,
                item.Rule.ApplicationMode, item.Rule.RuleType,
                item.Rule.CalculationStage, item.Rule.Priority, item.Rule.CalculationMethod,
                item.Rule.BaseType, item.Rule.Rate, item.Rule.FixedAmount, item.Rule.MinimumBase,
                item.Rule.MaximumBase, item.Rule.AppliesTo, item.Rule.EffectiveFrom, item.Rule.EffectiveTo,
                item.Summary,
                item.Targets.Select(evaluated =>
                {
                    var display = displays.GetValueOrDefault((evaluated.Target.TargetType, evaluated.Target.TargetId));
                    return new PayrollRuleEvaluationTargetDto(evaluated.Target.PayrollRuleTargetId,
                        evaluated.Target.TargetType, evaluated.Target.TargetId, display?.Name, display?.Code,
                        evaluated.Target.IsExcluded, evaluated.IsMatched);
                }).ToArray(), componentDisplays[item.Rule.PayrollComponentId].IsTaxable,
                componentDisplays[item.Rule.PayrollComponentId].IsStatutory,
                componentDisplays[item.Rule.PayrollComponentId].ContributionSide,
                componentDisplays[item.Rule.PayrollComponentId].SsoWageTreatment))
            .ToArray();

        return ServiceResult<PayrollRuleEvaluationDto>.Success(new(employee, period, orderedRules));
    }

    private static TargetEvaluation EvaluateTargets(IReadOnlyCollection<PayrollRuleTarget> targets, EvaluationContext context)
    {
        if (targets.Count == 0)
            return new(true, "No target records are configured; the rule applies to all employees.", []);

        var evaluated = targets.Select(target => new EvaluatedTarget(target, Matches(target, context))).ToArray();
        if (evaluated.Any(item => item.Target.IsExcluded && item.IsMatched))
            return new(false, string.Empty, evaluated);

        var inclusions = evaluated.Where(item => !item.Target.IsExcluded).ToArray();
        foreach (var group in inclusions.GroupBy(item => item.Target.TargetType, StringComparer.Ordinal))
        {
            // OR within one target type. Each type group must match, which implements AND across types.
            if (!group.Any(item => item.IsMatched))
                return new(false, string.Empty, evaluated);
        }

        var summary = inclusions.Length == 0
            ? "No inclusion targets are configured and no exclusion target matched."
            : "At least one inclusion target matched for each configured target type, and no exclusion target matched.";
        return new(true, summary, evaluated);
    }

    private static bool Matches(PayrollRuleTarget target, EvaluationContext context) => target.TargetType switch
    {
        "Employee" => target.TargetId == context.EmployeeId,
        "Department" => context.DepartmentId.HasValue && target.TargetId == context.DepartmentId.Value,
        "Designation" => context.DesignationId.HasValue && target.TargetId == context.DesignationId.Value,
        "EmploymentType" => context.EmploymentTypeId.HasValue && target.TargetId == context.EmploymentTypeId.Value,
        "Location" => context.LocationId.HasValue && target.TargetId == context.LocationId.Value,
        _ => false
    };

    private async Task<Dictionary<(string Type, Guid Id), TargetDisplay>> ResolveTargetDisplaysAsync(
        IReadOnlyCollection<PayrollRuleTarget> targets, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string Type, Guid Id), TargetDisplay>();
        var employeeIds = targets.Where(item => item.TargetType == "Employee").Select(item => item.TargetId).Distinct().ToArray();
        if (employeeIds.Length > 0)
        {
            var rows = await db.Employees.AsNoTracking().Where(item => employeeIds.Contains(item.EmployeeId))
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

    private static ServiceResult<PayrollRuleEvaluationDto> NotFound(string message) => ServiceResult<PayrollRuleEvaluationDto>.Fail("not_found", message);
    private sealed record EvaluationContext(Guid EmployeeId,
        Guid? DepartmentId, Guid? DesignationId, Guid? EmploymentTypeId, Guid? LocationId);
    private sealed record EvaluatedTarget(PayrollRuleTarget Target, bool IsMatched);
    private sealed record TargetEvaluation(bool IsApplicable, string Summary, IReadOnlyList<EvaluatedTarget> Targets);
    private sealed record ApplicableRuleCandidate(PayrollRule Rule, string Summary, IReadOnlyList<EvaluatedTarget> Targets);
    private sealed record TargetDisplay(string Name, string? Code);
}
