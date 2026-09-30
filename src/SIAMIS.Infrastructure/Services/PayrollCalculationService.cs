using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Domain.Entities.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Contains the single in-memory payroll calculation implementation used by preview and generation.</summary>
public sealed class PayrollCalculationService : IPayrollCalculationService
{
    private const decimal MaximumAmount = 999_999_999_999_999.9999m;

    public PayrollCalculationResult Calculate(PayrollCalculationEmployee employee, decimal basicSalary,
        IReadOnlyList<EmployeePayrollComponentAssignment> assignments, PayrollComponent basicSalaryComponent)
    {
        try
        {
            var roundedBasicSalary = RoundAmount(basicSalary);
            if (!ValidLineAmount(roundedBasicSalary))
                return Fail(employee, "Applicable BasicSalary must be positive and within the supported payroll amount range to create a Basic Salary line.");

            var lines = new List<PayrollCalculatedLine>
            {
                new(basicSalaryComponent.Id, basicSalaryComponent.Code!, basicSalaryComponent.Name, "Earning",
                    "Compensation", null, null, null, roundedBasicSalary, "Basic Salary from applicable EmployeeCompensation.")
            };
            var grossEarnings = roundedBasicSalary;
            foreach (var assignment in assignments.Where(item => item.PayrollComponent.Category == "Earning"))
            {
                var calculation = CalculateLineAmount(assignment, grossEarnings, roundedBasicSalary, null, true);
                if (calculation.Failure is not null) return Fail(employee, calculation.Failure);
                lines.Add(ToLine(assignment, calculation.Amount));
                grossEarnings = checked(grossEarnings + calculation.Amount);
                if (!ValidAmount(grossEarnings)) return Fail(employee, "GrossPay exceeds the supported decimal(19,4) range.");
            }

            var grossPay = RoundAmount(grossEarnings);
            var totalDeductions = 0m;
            foreach (var assignment in assignments.Where(item => item.PayrollComponent.Category == "Deduction"))
            {
                var calculation = CalculateLineAmount(assignment, grossEarnings, roundedBasicSalary, grossPay, false);
                if (calculation.Failure is not null) return Fail(employee, calculation.Failure);
                lines.Add(ToLine(assignment, calculation.Amount));
                totalDeductions = checked(totalDeductions + calculation.Amount);
                if (!ValidAmount(totalDeductions)) return Fail(employee, "TotalDeductions exceeds the supported decimal(19,4) range.");
            }

            var netPay = checked(grossPay - totalDeductions);
            if (netPay < 0) return Fail(employee, "Total deductions exceed GrossPay; payroll was not generated.");
            netPay = RoundAmount(netPay);
            if (!ValidAmount(grossPay) || !ValidAmount(totalDeductions) || !ValidAmount(netPay))
                return Fail(employee, "Calculated payroll totals exceed the supported decimal(19,4) range.");
            return new(employee, "Calculated", "Payroll calculated.", roundedBasicSalary, grossPay, totalDeductions, netPay, lines, null);
        }
        catch (OverflowException)
        {
            return Fail(employee, "A payroll calculation exceeded the supported decimal(19,4) range.");
        }
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
            assignment.PayrollComponent.PercentageBase, assignment.Quantity, assignment.Rate, amount, assignment.Remarks);

    private static string? NormalizePercentageBase(string? value) => value?.Trim() switch
    {
        { } baseValue when baseValue.Equals("BasicSalary", StringComparison.OrdinalIgnoreCase) => "BasicSalary",
        { } baseValue when baseValue.Equals("GrossEarnings", StringComparison.OrdinalIgnoreCase) => "GrossEarnings",
        { } baseValue when baseValue.Equals("GrossPay", StringComparison.OrdinalIgnoreCase) => "GrossPay",
        _ => null
    };

    private static PayrollCalculationResult Fail(PayrollCalculationEmployee employee, string message)
        => new(employee, "Failed", message, 0, 0, 0, 0, [], message);
    private static decimal RoundAmount(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    private static bool ValidAmount(decimal value) => value >= 0 && value <= MaximumAmount && decimal.Round(value, 4) == value;
    private static bool ValidLineAmount(decimal value) => value > 0 && ValidAmount(value);
}
