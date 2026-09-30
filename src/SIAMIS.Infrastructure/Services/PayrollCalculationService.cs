using System.Globalization;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Contains the single in-memory payroll calculation implementation used by preview and generation.</summary>
public sealed class PayrollCalculationService : IPayrollCalculationService
{
    private const decimal MaximumAmount = 999_999_999_999_999.9999m;

    public PayrollCalculationResult Calculate(PayrollCalculationEmployee employee, decimal basicSalary,
        IReadOnlyList<EmployeePayrollComponentAssignment> assignments, PayrollComponent basicSalaryComponent,
        IReadOnlyList<ApplicablePayrollRuleDto> applicableRules)
    {
        try
        {
            var roundedBasicSalary = RoundAmount(basicSalary);
            if (!ValidLineAmount(roundedBasicSalary))
                return Fail(employee, "Applicable BasicSalary must be positive and within the supported payroll amount range to create a Basic Salary line.");

            var lines = new List<PayrollCalculatedLine>
            {
                new(basicSalaryComponent.Id, basicSalaryComponent.Code!, basicSalaryComponent.Name, "Earning",
                    "Compensation", null, null, null, roundedBasicSalary, "Basic Salary from applicable EmployeeCompensation.",
                    SourceType: "BasicSalary", IsTaxableSnapshot: basicSalaryComponent.IsTaxable,
                    IsStatutorySnapshot: basicSalaryComponent.IsStatutory,
                    ContributionSideSnapshot: basicSalaryComponent.ContributionSide)
            };
            var grossEarnings = roundedBasicSalary;
            var earningRules = applicableRules.Where(rule => rule.CalculationStage == "Earning").ToArray();
            var duplicateReplacement = earningRules.Where(rule => rule.ApplicationMode == "ReplaceAssignment")
                .GroupBy(rule => rule.PayrollComponentId).FirstOrDefault(group => group.Count() > 1);
            if (duplicateReplacement is not null)
                return Fail(employee, $"Multiple applicable ReplaceAssignment earning rules target payroll component '{duplicateReplacement.First().PayrollComponentName}': {string.Join(", ", duplicateReplacement.Select(rule => rule.Code))}.");
            var replacedComponentIds = earningRules.Where(rule => rule.ApplicationMode == "ReplaceAssignment")
                .Select(rule => rule.PayrollComponentId).ToHashSet();
            foreach (var assignment in assignments.Where(item => item.PayrollComponent.Category == "Earning"
                && !replacedComponentIds.Contains(item.PayrollComponentId)))
            {
                var calculation = CalculateLineAmount(assignment, grossEarnings, roundedBasicSalary, null, true);
                if (calculation.Failure is not null) return Fail(employee, calculation.Failure);
                lines.Add(ToLine(assignment, calculation.Amount));
                grossEarnings = checked(grossEarnings + calculation.Amount);
                if (!ValidAmount(grossEarnings)) return Fail(employee, "GrossPay exceeds the supported decimal(19,4) range.");
            }

            var skippedRules = new List<string>();
            foreach (var rule in earningRules)
            {
                var calculation = CalculateRuleLine(rule, roundedBasicSalary, grossEarnings, null);
                if (calculation.Failure is not null) return Fail(employee, calculation.Failure);
                if (calculation.Skipped is not null) { skippedRules.Add(calculation.Skipped); continue; }
                lines.Add(calculation.Line!);
                grossEarnings = checked(grossEarnings + calculation.Line!.Amount);
                if (!ValidAmount(grossEarnings)) return Fail(employee, "GrossPay exceeds the supported decimal(19,4) range.");
            }

            var grossPay = RoundAmount(grossEarnings);
            var totalDeductions = 0m;
            var deductionRules = applicableRules.Where(rule => rule.CalculationStage == "Deduction").ToArray();
            var duplicateDeductionReplacement = deductionRules.Where(rule => rule.ApplicationMode == "ReplaceAssignment")
                .GroupBy(rule => rule.PayrollComponentId).FirstOrDefault(group => group.Count() > 1);
            if (duplicateDeductionReplacement is not null)
                return Fail(employee, $"Multiple applicable ReplaceAssignment deduction rules target payroll component '{duplicateDeductionReplacement.First().PayrollComponentName}': {string.Join(", ", duplicateDeductionReplacement.Select(rule => rule.Code))}.");
            var replacedDeductionComponentIds = deductionRules.Where(rule => rule.ApplicationMode == "ReplaceAssignment")
                .Select(rule => rule.PayrollComponentId).ToHashSet();
            foreach (var assignment in assignments.Where(item => item.PayrollComponent.Category == "Deduction"
                && !replacedDeductionComponentIds.Contains(item.PayrollComponentId)))
            {
                var calculation = CalculateLineAmount(assignment, grossEarnings, roundedBasicSalary, grossPay, false);
                if (calculation.Failure is not null) return Fail(employee, calculation.Failure);
                lines.Add(ToLine(assignment, calculation.Amount));
                totalDeductions = checked(totalDeductions + calculation.Amount);
                if (!ValidAmount(totalDeductions)) return Fail(employee, "TotalDeductions exceeds the supported decimal(19,4) range.");
            }
            foreach (var rule in deductionRules)
            {
                var calculation = CalculateRuleLine(rule, roundedBasicSalary, grossEarnings, grossPay);
                if (calculation.Failure is not null) return Fail(employee, calculation.Failure);
                if (calculation.Skipped is not null) { skippedRules.Add(calculation.Skipped); continue; }
                lines.Add(calculation.Line!);
                totalDeductions = checked(totalDeductions + calculation.Line!.Amount);
                if (!ValidAmount(totalDeductions)) return Fail(employee, "TotalDeductions exceeds the supported decimal(19,4) range.");
            }

            var netPay = checked(grossPay - totalDeductions);
            if (netPay < 0) return Fail(employee, "Total deductions exceed GrossPay; payroll was not generated.");
            netPay = RoundAmount(netPay);
            if (!ValidAmount(grossPay) || !ValidAmount(totalDeductions) || !ValidAmount(netPay))
                return Fail(employee, "Calculated payroll totals exceed the supported decimal(19,4) range.");
            // Sum final, already-rounded employee earning lines; deductions and contribution-side classification do not affect this total.
            var taxableEarnings = lines.Where(line => line.ComponentType == "Earning" && line.IsTaxableSnapshot)
                .Sum(line => line.Amount);
            return new(employee, "Calculated", "Payroll calculated.", roundedBasicSalary, grossPay, totalDeductions,
                netPay, taxableEarnings, lines, null, skippedRules);
        }
        catch (OverflowException)
        {
            return Fail(employee, "A payroll calculation exceeded the supported decimal(19,4) range.");
        }
    }

    private static (PayrollCalculatedLine? Line, string? Skipped, string? Failure) CalculateRuleLine(
        ApplicablePayrollRuleDto rule, decimal basicSalary, decimal grossEarnings, decimal? grossPay)
    {
        if (rule.ApplicationMode is not ("Supplement" or "ReplaceAssignment"))
            return (null, null, $"Payroll rule '{rule.Code}' has unsupported ApplicationMode '{rule.ApplicationMode}'.");
        if (rule.PayrollComponentType != rule.CalculationStage || string.IsNullOrWhiteSpace(rule.PayrollComponentCode))
            return (null, null, $"Payroll rule '{rule.Code}' does not reference a usable {rule.CalculationStage} payroll component.");
        if (rule.AppliesTo != "Employee")
            return (null, null, $"Payroll rule '{rule.Code}' has AppliesTo '{rule.AppliesTo}'; employer contribution calculation is not supported.");

        decimal amount;
        decimal? baseAmount = null;
        switch (rule.CalculationMethod)
        {
            case "FixedAmount":
                if (!rule.FixedAmount.HasValue || rule.MinimumBase.HasValue || rule.MaximumBase.HasValue)
                    return (null, null, $"Payroll rule '{rule.Code}' has invalid FixedAmount configuration.");
                amount = rule.FixedAmount.Value;
                break;
            case "Percentage":
                if (!rule.Rate.HasValue)
                    return (null, null, $"Payroll rule '{rule.Code}' requires Rate for Percentage calculation.");
                baseAmount = rule.BaseType switch
                {
                    "BasicSalary" => basicSalary,
                    "GrossEarnings" => grossEarnings,
                    "GrossPay" when rule.CalculationStage == "Deduction" => grossPay,
                    _ => null
                };
                if (!baseAmount.HasValue)
                    return (null, null, $"Payroll rule '{rule.Code}' has unsupported {rule.CalculationStage.ToLowerInvariant()} percentage BaseType '{rule.BaseType ?? "null"}'.");
                if ((rule.MinimumBase.HasValue && baseAmount.Value < rule.MinimumBase.Value)
                    || (rule.MaximumBase.HasValue && baseAmount.Value > rule.MaximumBase.Value))
                    return (null, $"Payroll rule '{rule.Code}' produced no line: actual {rule.BaseType} base {Format(baseAmount.Value)} is outside inclusive MinimumBase {Format(rule.MinimumBase)} / MaximumBase {Format(rule.MaximumBase)}.", null);
                amount = checked(baseAmount.Value * (rule.Rate.Value / 100m));
                break;
            case "Manual":
                return (null, null, $"Payroll rule '{rule.Code}' uses Manual calculation, but no numeric amount source is configured.");
            default:
                return (null, null, $"Payroll rule '{rule.Code}' has unsupported CalculationMethod '{rule.CalculationMethod}'.");
        }

        amount = RoundAmount(amount);
        if (!ValidLineAmount(amount))
            return (null, null, $"Payroll rule '{rule.Code}' calculates to an amount that cannot be stored as a positive payroll line.");
        var remarks = $"PayrollRuleId={rule.PayrollRuleId}; Code={rule.Code}; Name={rule.Name}; ApplicationMode={rule.ApplicationMode}; CalculationMethod={rule.CalculationMethod}; BaseType={rule.BaseType ?? "null"}; BaseAmount={Format(baseAmount)}; MinimumBase={Format(rule.MinimumBase)}; MaximumBase={Format(rule.MaximumBase)}; Rate={Format(rule.Rate)}; Amount={Format(amount)}";
        var line = new PayrollCalculatedLine(rule.PayrollComponentId, rule.PayrollComponentCode!, rule.PayrollComponentName,
            rule.CalculationStage, rule.CalculationMethod, rule.BaseType, null, rule.Rate, amount, remarks,
            rule.PayrollRuleId, rule.Code, rule.Name, rule.ApplicationMode, rule.BaseType, baseAmount,
            rule.MinimumBase, rule.MaximumBase, "PayrollRule", rule.PayrollRuleId,
            rule.PayrollComponentIsTaxable, rule.PayrollComponentIsStatutory, rule.PayrollComponentContributionSide);
        return (line, null, null);
    }

    private static (decimal Amount, string? Failure) CalculateLineAmount(
        EmployeePayrollComponentAssignment assignment, decimal grossEarnings, decimal basicSalary, decimal? grossPay, bool isEarning)
    {
        var component = assignment.PayrollComponent;
        decimal amount;
        switch (component.CalculationMethod)
        {
            case "FixedAmount":
            case "Manual":
                amount = assignment.Amount;
                break;
            case "QuantityRate":
                if (!assignment.Quantity.HasValue || !assignment.Rate.HasValue)
                    return (0, $"Payroll component '{component.Name}' uses QuantityRate and requires both Quantity and Rate.");
                amount = checked(assignment.Quantity.Value * assignment.Rate.Value);
                break;
            case "Percentage":
                if (!assignment.Rate.HasValue)
                    return (0, $"Payroll component '{component.Name}' uses Percentage and requires Rate.");
                var percentageBase = NormalizePercentageBase(component.PercentageBase);
                if (percentageBase is null)
                    return (0, $"Payroll component '{component.Name}' has missing or invalid PercentageBase configuration.");
                decimal baseAmount = percentageBase switch
                {
                    "BasicSalary" => basicSalary,
                    "GrossEarnings" => grossEarnings,
                    "GrossPay" when !isEarning && grossPay.HasValue => grossPay.Value,
                    "GrossPay" when isEarning => -1,
                    _ => -1
                };
                if (baseAmount < 0)
                    return (0, $"Payroll component '{component.Name}' cannot safely use GrossPay as an earning percentage base because that would be circular.");
                amount = checked(baseAmount * (assignment.Rate.Value / 100m));
                break;
            default:
                return (0, $"Payroll component '{component.Name}' has unsupported CalculationMethod '{component.CalculationMethod}'.");
        }

        amount = RoundAmount(amount);
        return ValidLineAmount(amount)
            ? (amount, null)
            : (0, $"Payroll component '{component.Name}' calculates to an amount that cannot be stored as a positive payroll line.");
    }

    private static PayrollCalculatedLine ToLine(EmployeePayrollComponentAssignment assignment, decimal amount)
        => new(assignment.PayrollComponentId, assignment.PayrollComponent.Code!, assignment.PayrollComponent.Name,
            assignment.PayrollComponent.Category, assignment.PayrollComponent.CalculationMethod,
            assignment.PayrollComponent.PercentageBase, assignment.Quantity, assignment.Rate, amount, assignment.Remarks,
            SourceType: "Assignment", SourceId: assignment.EmployeePayrollComponentAssignmentId,
            IsTaxableSnapshot: assignment.PayrollComponent.IsTaxable,
            IsStatutorySnapshot: assignment.PayrollComponent.IsStatutory,
            ContributionSideSnapshot: assignment.PayrollComponent.ContributionSide);

    private static string? NormalizePercentageBase(string? value) => value?.Trim() switch
    {
        { } baseValue when baseValue.Equals("BasicSalary", StringComparison.OrdinalIgnoreCase) => "BasicSalary",
        { } baseValue when baseValue.Equals("GrossEarnings", StringComparison.OrdinalIgnoreCase) => "GrossEarnings",
        { } baseValue when baseValue.Equals("GrossPay", StringComparison.OrdinalIgnoreCase) => "GrossPay",
        _ => null
    };

    private static PayrollCalculationResult Fail(PayrollCalculationEmployee employee, string message)
        => new(employee, "Failed", message, 0, 0, 0, 0, 0, [], message);
    private static string Format(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    private static decimal RoundAmount(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    private static bool ValidAmount(decimal value) => value >= 0 && value <= MaximumAmount && decimal.Round(value, 4) == value;
    private static bool ValidLineAmount(decimal value) => value > 0 && ValidAmount(value);
}
