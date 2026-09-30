using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.MasterData;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Calculates payroll previews from current inputs without changing persisted data.</summary>
public sealed class PayrollPreviewService(SIAMISDbContext db, IPayrollCalculationService calculator,
    IPayrollRuleEvaluator ruleEvaluator) : IPayrollPreviewService
{
    public async Task<ServiceResult<PayrollPreviewSummary>> PreviewAsync(
        Guid payrollPeriodId, PayrollPreviewRequest request, CancellationToken cancellationToken)
    {
        var period = await db.PayrollPeriods.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PayrollPeriodId == payrollPeriodId, cancellationToken);
        if (period is null) return Fail("not_found", "Payroll period was not found.");
        if (period.Status is not ("Open" or "Processing"))
            return Fail("conflict", $"Payroll preview is allowed only for Open or Processing periods. Current status: {period.Status}.");

        var requestedIds = request.EmployeeIds?.Distinct().ToArray();
        if (requestedIds is { Length: > 0 })
        {
            var foundIds = await db.Employees.AsNoTracking().Where(item => requestedIds.Contains(item.EmployeeId))
                .Select(item => item.EmployeeId).ToListAsync(cancellationToken);
            var missing = requestedIds.Except(foundIds).ToArray();
            if (missing.Length > 0) return Fail("validation", $"Every EmployeeId must exist. Not found: {string.Join(", ", missing)}.");
        }

        var employeeQuery = db.Employees.AsNoTracking().AsQueryable();
        employeeQuery = requestedIds is { Length: > 0 }
            ? employeeQuery.Where(item => requestedIds.Contains(item.EmployeeId))
            : employeeQuery.Where(item => item.IsActive);
        var employees = await employeeQuery.OrderBy(item => item.EmployeeNumber).ThenBy(item => item.EmployeeId)
            .Select(item => new EmployeeCandidate(item.EmployeeId, item.EmployeeNumber, item.FirstName, item.MiddleName, item.LastName, item.PreferredName, item.IsActive))
            .ToListAsync(cancellationToken);

        var basicSalaryComponents = await db.PayrollComponents.AsNoTracking()
            .Where(item => item.Name.Trim().ToLower() == "basic salary").Take(2).ToListAsync(cancellationToken);
        var basicSalaryComponent = basicSalaryComponents.Count == 1 && basicSalaryComponents[0].IsActive
            && basicSalaryComponents[0].Category == "Earning" && !string.IsNullOrWhiteSpace(basicSalaryComponents[0].Code)
            ? basicSalaryComponents[0] : null;
        var results = new List<PayrollPreviewEmployeeResult>(employees.Count);

        foreach (var employee in employees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = FormatName(employee);
            if (!employee.IsActive)
            {
                results.Add(Result(employee, name, "Skipped", "Employee is inactive."));
                continue;
            }
            if (basicSalaryComponent is null)
            {
                var detail = basicSalaryComponents.Count != 1
                    ? $"Payroll preview requires exactly one PayrollComponent named 'Basic Salary'; found {basicSalaryComponents.Count}."
                    : "The existing Basic Salary component must be active, have a code, and be an Earning component.";
                results.Add(Result(employee, name, "Failed", detail));
                continue;
            }

            var compensation = await db.EmployeeCompensations.AsNoTracking()
                .Where(item => item.EmployeeId == employee.EmployeeId && item.EffectiveFrom <= period.StartDate
                    && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= period.StartDate))
                .OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.EmployeeCompensationId)
                .FirstOrDefaultAsync(cancellationToken);
            if (compensation is null)
            {
                results.Add(Result(employee, name, "Skipped", "No applicable compensation found for payroll period start date."));
                continue;
            }

            var assignments = await db.EmployeePayrollComponentAssignments.AsNoTracking().Include(item => item.PayrollComponent)
                .Where(item => item.EmployeeId == employee.EmployeeId && item.EffectiveFrom <= period.StartDate
                    && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= period.StartDate))
                .OrderBy(item => item.PayrollComponent.Category).ThenBy(item => item.PayrollComponent.Code).ThenBy(item => item.PayrollComponent.Id)
                .ToListAsync(cancellationToken);
            var inactive = assignments.FirstOrDefault(item => !item.PayrollComponent.IsActive);
            if (inactive is not null)
            {
                results.Add(Result(employee, name, "Failed", $"Assignment references inactive payroll component '{inactive.PayrollComponent.Name}'."));
                continue;
            }
            if (assignments.Any(item => item.PayrollComponentId == basicSalaryComponent.Id))
            {
                results.Add(Result(employee, name, "Failed", "Basic Salary is generated from EmployeeCompensation and must not also have an employee component assignment."));
                continue;
            }

            var evaluation = await ruleEvaluator.EvaluateApplicableRulesAsync(payrollPeriodId, employee.EmployeeId, cancellationToken);
            if (evaluation.Failure is not null)
            {
                results.Add(Result(employee, name, "Failed", $"Payroll rule evaluation failed: {evaluation.Failure.Message}"));
                continue;
            }
            var calculation = calculator.Calculate(new PayrollCalculationEmployee(employee.EmployeeId, employee.EmployeeNumber, name),
                compensation.BasicSalary, assignments, basicSalaryComponent, evaluation.Value!.ApplicableRules);
            if (calculation.Status != "Calculated")
            {
                results.Add(Result(employee, name, "Failed", calculation.Message));
                continue;
            }

            results.Add(new PayrollPreviewEmployeeResult(employee.EmployeeId, employee.EmployeeNumber, name, "Calculated",
                calculation.BasicSalary, calculation.GrossPay, calculation.TotalDeductions, calculation.NetPay, calculation.TaxableEarnings,
                calculation.SkippedRuleExplanations is { Count: > 0 }
                    ? $"Payroll preview calculated. {string.Join(" ", calculation.SkippedRuleExplanations)}"
                    : "Payroll preview calculated.",
                calculation.Lines.Select(line => new PayrollPreviewLineDto(line.PayrollComponentId, line.ComponentCode,
                    line.ComponentName, line.ComponentType, line.CalculationMethod, line.PercentageBase,
                    line.Quantity, line.Rate, line.Amount, line.Remarks, line.PayrollRuleId, line.RuleCode,
                    line.RuleName, line.ApplicationMode, line.BaseType, line.BaseAmount,
                    line.MinimumBase, line.MaximumBase, line.SourceType, line.SourceId,
                    line.IsTaxableSnapshot, line.IsStatutorySnapshot, line.ContributionSideSnapshot)).ToArray()));
        }

        return ServiceResult<PayrollPreviewSummary>.Success(new PayrollPreviewSummary(payrollPeriodId, results.Count,
            results.Count(item => item.Status == "Calculated"), results.Count(item => item.Status == "Skipped"),
            results.Count(item => item.Status == "Failed"), results));
    }

    private static PayrollPreviewEmployeeResult Result(EmployeeCandidate employee, string name, string status, string message)
        => new(employee.EmployeeId, employee.EmployeeNumber, name, status, null, null, null, null, null, message, []);

    private static string FormatName(EmployeeCandidate employee)
        => string.Join(' ', new[] { string.IsNullOrWhiteSpace(employee.PreferredName) ? employee.FirstName : employee.PreferredName, employee.MiddleName, employee.LastName }
            .Where(item => !string.IsNullOrWhiteSpace(item)));

    private static ServiceResult<PayrollPreviewSummary> Fail(string code, string message) => ServiceResult<PayrollPreviewSummary>.Fail(code, message);
    private sealed record EmployeeCandidate(Guid EmployeeId, string EmployeeNumber, string FirstName, string? MiddleName, string LastName, string? PreferredName, bool IsActive);
}
